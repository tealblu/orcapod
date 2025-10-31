using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OrcaPod.Utils
{
    public sealed class FileChangedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public WatcherChangeTypes ChangeType { get; }

        public FileChangedEventArgs(string filePath, WatcherChangeTypes changeType)
        {
            FilePath = filePath;
            ChangeType = changeType;
        }
    }

    public sealed class Watchdog : IDisposable
    {
        // Public event raised when a watched file changes
        public event EventHandler<FileChangedEventArgs>? FileChanged;

        // Minimum interval between raising events for the same file (debounce)
        public TimeSpan DebounceInterval { get; set; } = TimeSpan.FromMilliseconds(150);

        // Internal structures:
        // directory -> set of file names (lowercase) being watched in that directory
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _directoryFiles
            = new(StringComparer.OrdinalIgnoreCase);

        // directory -> FileSystemWatcher
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers
            = new(StringComparer.OrdinalIgnoreCase);

        // file path -> last event time (for debouncing)
        private readonly ConcurrentDictionary<string, DateTime> _lastEventTime
            = new(StringComparer.OrdinalIgnoreCase);

        private volatile bool _running;
        private bool _disposed;

        public bool IsRunning => _running;

        public Watchdog() { }

        // Start watching (turn on all existing watchers)
        public void Start()
        {
            ThrowIfDisposed();
            if (_running) return;
            foreach (var kvp in _watchers)
            {
                kvp.Value.EnableRaisingEvents = true;
            }
            _running = true;
        }

        // Stop watching (disable raising events)
        public void Stop()
        {
            ThrowIfDisposed();
            if (!_running) return;
            foreach (var kvp in _watchers)
            {
                kvp.Value.EnableRaisingEvents = false;
            }
            _running = false;
        }

        // Add a single file to watch (path can be relative or absolute)
        public void AddFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath");
            ThrowIfDisposed();

            var fullPath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Invalid file path");
            var fileName = Path.GetFileName(fullPath);

            Console.WriteLine($"Watchdog: Adding file to watch: {fullPath}");

            var fileSet = _directoryFiles.GetOrAdd(dir, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
            fileSet.TryAdd(fileName, 0);

            // Ensure watcher exists for directory
            _watchers.GetOrAdd(dir, d =>
            {
                var watcher = new FileSystemWatcher(d)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.Attributes
                };
                watcher.Changed += OnFsEvent;
                watcher.Created += OnFsEvent;
                watcher.Renamed += OnFsRenamed;
                watcher.Deleted += OnFsEvent;
                watcher.Error += OnFsError;

                watcher.EnableRaisingEvents = _running;
                return watcher;
            });
        }

        // Add multiple files
        public void AddFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null) throw new ArgumentNullException(nameof(filePaths));
            foreach (var f in filePaths) AddFile(f);
        }

        // Remove a file from watching
        public bool RemoveFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath");
            ThrowIfDisposed();

            var fullPath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Invalid file path");
            var fileName = Path.GetFileName(fullPath);

            if (_directoryFiles.TryGetValue(dir, out var fileSet))
            {
                fileSet.TryRemove(fileName, out _);

                // if no files left in directory, remove and dispose watcher
                if (fileSet.IsEmpty)
                {
                    _directoryFiles.TryRemove(dir, out _);
                    if (_watchers.TryRemove(dir, out var watcher))
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                }
                _lastEventTime.TryRemove(fullPath, out _);
                return true;
            }
            return false;
        }

        // Remove multiple files
        public void RemoveFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null) throw new ArgumentNullException(nameof(filePaths));
            foreach (var f in filePaths) RemoveFile(f);
        }

        // Remove all files and dispose watchers
        public void RemoveAll()
        {
            ThrowIfDisposed();
            foreach (var kvp in _watchers)
            {
                kvp.Value.EnableRaisingEvents = false;
                kvp.Value.Dispose();
            }
            _watchers.Clear();
            _directoryFiles.Clear();
            _lastEventTime.Clear();
        }

        private void OnFsError(object sender, ErrorEventArgs e)
        {
            // FileSystemWatcher can fail (buffer overflow). In that case, we can clear and recreate watchers for safety.
            // For simplicity: stop, clear caches, and restart if running.
            try
            {
                Stop();
                foreach (var kvp in _watchers)
                {
                    kvp.Value.EnableRaisingEvents = false;
                    kvp.Value.Dispose();
                }
                _watchers.Clear();
                var directories = _directoryFiles.Keys.ToArray();
                foreach (var dir in directories)
                {
                    // Recreate watcher if still have files in that dir
                    if (_directoryFiles.TryGetValue(dir, out var set) && !set.IsEmpty)
                    {
                        _watchers.GetOrAdd(dir, d =>
                        {
                            var watcher = new FileSystemWatcher(d)
                            {
                                IncludeSubdirectories = false,
                                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.Attributes
                            };
                            watcher.Changed += OnFsEvent;
                            watcher.Created += OnFsEvent;
                            watcher.Renamed += OnFsRenamed;
                            watcher.Deleted += OnFsEvent;
                            watcher.Error += OnFsError;
                            watcher.EnableRaisingEvents = false;
                            return watcher;
                        });
                    }
                }
                if (_running) Start();
            }
            catch
            {
                // swallow to avoid throwing from event thread
            }
        }

        private void OnFsRenamed(object sender, RenamedEventArgs e)
        {
            // Renames may change which file we're watching; treat both old and new names.
            HandleEvent(e.FullPath, e.ChangeType);
            HandleEvent(e.OldFullPath, e.ChangeType);
        }

        private void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            HandleEvent(e.FullPath, e.ChangeType);
        }

        private void HandleEvent(string fullPath, WatcherChangeTypes changeType)
        {
            try
            {
                // Normalize
                if (string.IsNullOrEmpty(fullPath)) return;
                var path = Path.GetFullPath(fullPath);
                var dir = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);
                if (dir == null || fileName == null) return;

                // Check if this file is being watched
                if (!_directoryFiles.TryGetValue(dir, out var fileSet)) return;
                if (!fileSet.ContainsKey(fileName)) return;

                // Debounce: avoid flooding duplicate events
                var now = DateTime.UtcNow;
                var last = _lastEventTime.GetOrAdd(path, DateTime.MinValue);
                if (now - last < DebounceInterval)
                {
                    // update last time to the newest to prolong debounce window
                    _lastEventTime[path] = now;
                    return;
                }
                _lastEventTime[path] = now;

                // Raise event (catch exceptions from subscribers)
                FileChanged?.Invoke(this, new FileChangedEventArgs(path, changeType));
            }
            catch
            {
                // swallow to keep watcher alive
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(Watchdog));
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            foreach (var kvp in _watchers)
            {
                try
                {
                    kvp.Value.EnableRaisingEvents = false;
                    kvp.Value.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
            _directoryFiles.Clear();
            _lastEventTime.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal void TestWatchdog()
        {
            AddFile("../README.md");
            FileChanged += (s, e) =>
            {
                Console.WriteLine($"File changed: {e.FilePath}");
            };

            Start();

            // block until user hits Ctrl+C
            var stop = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true; // prevent immediate process termination
                Console.WriteLine("Stopping...");
                stop.Set();
            };

            Console.WriteLine("Watching files. Press Ctrl+C to exit.");
            stop.Wait();
        }
    }
}