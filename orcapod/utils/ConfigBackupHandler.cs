using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrcaPod.Utils
{
    public static class ConfigBackupHandler
    {
        private static Microsoft.Extensions.Logging.ILogger<OrcaPod.Service.MainService>? _logger;
        private static Dictionary<string, string> _mappings = new Dictionary<string, string>();

        public static void Initialize(Microsoft.Extensions.Logging.ILogger<OrcaPod.Service.MainService> logger)
        {
            _logger = logger;
            _mappings = LoadMappings();
        }

        public static Dictionary<string, string> LoadMappings()
        {
            if (_logger == null)
                throw new InvalidOperationException("ConfigBackupHandler not initialized. Call Initialize() first.");
            _logger.LogInformation("Loading backup mappings");
            var section = SettingsHandler.GetSection("Mappings");
            Dictionary<string, string> mappings = new Dictionary<string, string>();
            if (section != null)
            {
                // Use AsEnumerable to get all key-value pairs, including keys with backslashes
                foreach (var kvp in section.AsEnumerable())
                {
                    // Skip the parent section itself
                    if (kvp.Value != null && kvp.Key != "Mappings")
                    {
                        // Remove the "Mappings:" prefix to get the original key
                        var key = kvp.Key.StartsWith("Mappings:") ? kvp.Key.Substring("Mappings:".Length) : kvp.Key;
                        mappings[key] = kvp.Value;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Mappings section not found in configuration.");
            }
            foreach (var kvp in mappings)
            {
                _logger.LogInformation($"Mapping loaded: {kvp.Key} -> {kvp.Value}");
            }
            return mappings;
        }

        public static void BackupConfigs()
        {
            if (_logger == null)
                throw new InvalidOperationException("ConfigBackupHandler not initialized. Call Initialize() first.");
            foreach (var mapping in _mappings)
            {
                BackupConfig(mapping.Key);
            }
        }

        public static void BackupConfig(string sourcePath)
        {
            if (_logger == null)
                throw new InvalidOperationException("ConfigBackupHandler not initialized. Call Initialize() first.");
            if (_mappings.TryGetValue(sourcePath, out var destinationPath))
            {
                try
                {
                    bool sourceIsDir = Directory.Exists(sourcePath);
                    bool destIsDir = Directory.Exists(destinationPath) || (sourceIsDir && !File.Exists(destinationPath));
                    if (sourceIsDir && destIsDir)
                    {
                        // Copy all files and subdirectories recursively
                        CopyDirectory(sourcePath, destinationPath, overwrite: true);
                        _logger.LogInformation($"Backed up directory '{sourcePath}' to '{destinationPath}'");
                    }
                    else
                    {
                        File.Copy(sourcePath, destinationPath, overwrite: true);
                        _logger.LogInformation($"Backed up file '{sourcePath}' to '{destinationPath}'");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to back up '{sourcePath}' to '{destinationPath}': {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning($"No backup mapping found for '{sourcePath}'");
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir, overwrite);
            }
    }

        public static bool CheckIfNewerBackupExists(string sourcePath)
        {
            if (_logger == null)
                throw new InvalidOperationException("ConfigBackupHandler not initialized. Call Initialize() first.");
            if (_mappings.TryGetValue(sourcePath, out var destinationPath))
            {
                bool sourceIsDir = Directory.Exists(sourcePath);
                bool destIsDir = Directory.Exists(destinationPath);
                if (sourceIsDir && destIsDir)
                {
                    // Recursively check if any file in destination is newer than its counterpart in source
                    var sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
                    foreach (var srcFile in sourceFiles)
                    {
                        var relativePath = Path.GetRelativePath(sourcePath, srcFile);
                        var destFile = Path.Combine(destinationPath, relativePath);
                        if (File.Exists(destFile))
                        {
                            var srcInfo = new FileInfo(srcFile);
                            var destInfo = new FileInfo(destFile);
                            if (destInfo.LastWriteTime > srcInfo.LastWriteTime)
                                return true;
                        }
                    }
                    return false;
                }
                else if (File.Exists(destinationPath))
                {
                    var sourceInfo = new FileInfo(sourcePath);
                    var destInfo = new FileInfo(destinationPath);
                    return destInfo.LastWriteTime > sourceInfo.LastWriteTime;
                }
            }
            return false;
        }

        public static void RestoreConfig(string sourcePath)
        {
            if (_logger == null)
                throw new InvalidOperationException("ConfigBackupHandler not initialized. Call Initialize() first.");
            if (_mappings.TryGetValue(sourcePath, out var destinationPath))
            {
                try
                {
                    bool sourceIsDir = Directory.Exists(sourcePath);
                    bool destIsDir = Directory.Exists(destinationPath);
                    if (sourceIsDir && destIsDir)
                    {
                        // Restore all files and subdirectories recursively
                        RestoreDirectory(destinationPath, sourcePath, overwrite: true);
                        _logger.LogInformation($"Restored directory '{sourcePath}' from '{destinationPath}'");
                    }
                    else
                    {
                        File.Copy(destinationPath, sourcePath, overwrite: true);
                        _logger.LogInformation($"Restored file '{sourcePath}' from '{destinationPath}'");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to restore '{sourcePath}' from '{destinationPath}': {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning($"No backup mapping found for '{sourcePath}'");
            }
        }

            // Helper method to restore directories recursively
            private static void RestoreDirectory(string sourceDir, string destDir, bool overwrite)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                RestoreDirectory(dir, destSubDir, overwrite);
            }
    }

        public static void SyncConfigs()
        {
            if (_logger == null)
                throw new InvalidOperationException("ConfigBackupHandler not initialized. Call Initialize() first.");
            _logger.LogInformation("Syncing configurations with backups");
            foreach (var mapping in _mappings)
            {
                var sourcePath = mapping.Key;
                if (CheckIfNewerBackupExists(sourcePath))
                {
                    _logger.LogInformation($"Newer backup found for '{sourcePath}', restoring.");
                    RestoreConfig(sourcePath);
                }
                else {
                    _logger.LogInformation($"No newer backup found for '{sourcePath}', backing up.");
                    BackupConfig(sourcePath);
                }
            }
        }

        public static void Dispose()
        {
            // Cleanup if necessary
            _logger = null;
            _mappings.Clear();
        }
    }
}