using System;
using System.Runtime.Versioning;
using OrcaPod.Utils;
using Microsoft.Extensions.Configuration;

namespace Orcapod.Utils
{
    public static class InstallHandler
    {
        private static bool _initialized = false;
        private static List<string> _programTargets = new List<string>();

        static InstallHandler()
        {
            Initialize();
        }

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            // Initialization logic for installation handling
            TryFindProgramTargets();
        }

        internal static void HandleInstall()
        {
            // check if running on windows or linux
#if WINDOWS
                InstallOnWindows();
#elif LINUX
            InstallOnLinux();
#endif
        }

        internal static void HandleUninstall()
        {
#if WINDOWS
            UninstallOnWindows();
#elif LINUX
            UninstallOnLinux();
#endif
        }

        [SupportedOSPlatform("windows")]
        private static void InstallOnWindows()
        {
            foreach (var target in _programTargets)
            {
                try
                {
                    // get the path to the target's shortcut in both user and common Start Menu
                    string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                    string programsPath = Path.Combine(startMenuPath, "Programs");
                    string commonStartMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                    string commonProgramsPath = Path.Combine(commonStartMenuPath, "Programs");

                    var shortcutFiles = new List<string>();
                    if (Directory.Exists(programsPath))
                    {
                        shortcutFiles.AddRange(Directory.GetFiles(programsPath, "*.lnk", SearchOption.AllDirectories)
                            .Where(f => Path.GetFileNameWithoutExtension(f).Contains(target, StringComparison.OrdinalIgnoreCase)));
                    }
                    if (Directory.Exists(commonProgramsPath))
                    {
                        shortcutFiles.AddRange(Directory.GetFiles(commonProgramsPath, "*.lnk", SearchOption.AllDirectories)
                            .Where(f => Path.GetFileNameWithoutExtension(f).Contains(target, StringComparison.OrdinalIgnoreCase)));
                    }

                    string? shortcutPath = shortcutFiles.FirstOrDefault();

                    if (shortcutPath != null)
                    {
                        LogHandler.LogInfo($"Found shortcut for {target}: {shortcutPath}");
                        InjectLaunchWrapper(shortcutPath);
                    }
                    else
                    {
                        throw new FileNotFoundException($"Shortcut for {target} not found in Start Menu.");
                    }
                }
                catch (Exception ex)
                {
                    LogHandler.LogError($"Failed to install autostart for {target}: {ex.Message}");
                }
            }

            [SupportedOSPlatform("windows")]
            void InjectLaunchWrapper(string shortcutPath)
            {
                LogHandler.LogInfo($"Injecting launch wrapper into shortcut: {shortcutPath}");

                    // get path for orcapod executable
                    string orcapodPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    string shortcutDir = Path.GetDirectoryName(shortcutPath) ?? "";

                    // use dynamic com interop to modify the shortcut to point to launch_wrapper.bat
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType == null)
                    {
                        LogHandler.LogError("Failed to get WScript.Shell COM type.");
                        return;
                    }
                    var shellInstance = Activator.CreateInstance(shellType);
                    if (shellInstance == null)
                    {
                        LogHandler.LogError("Failed to create WScript.Shell COM instance.");
                        return;
                    }
                    dynamic shell = shellInstance;

                    dynamic shortcut = shell.CreateShortcut(shortcutPath);

                    string originalTarget = shortcut.TargetPath;
                    LogHandler.LogInfo($"Original shortcut target: {originalTarget}");

                    // if the shortcut already points to the launch wrapper, skip
                    if (originalTarget.EndsWith("launch_wrapper.bat", StringComparison.OrdinalIgnoreCase))
                    {
                        LogHandler.LogInfo("Shortcut already points to launch wrapper. Skipping injection.");
                        return;
                    }

                    // create the launch wrapper batch file
                    string launchWrapperPath = Path.Combine(shortcutDir, "launch_wrapper.bat");
                    try
                    {
                        using (var writer = new StreamWriter(launchWrapperPath))
                        {
                            writer.WriteLine($"@echo off");
                            writer.WriteLine($"start \"\" \"{originalTarget}\"");
                            writer.WriteLine($"start \"\" \"{orcapodPath}\"");
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
#if WINDOWS
                        LogHandler.LogError($"Access denied when writing launch wrapper. Attempting to relaunch with admin rights.");
                        try
                        {
                            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true,
                                Verb = "runas"
                            };
                            System.Diagnostics.Process.Start(psi);
                            LogHandler.LogInfo("Process relaunched with admin rights. Exiting current instance.");
                            Environment.Exit(1);
                        }
                        catch (Exception elevateEx)
                        {
                            LogHandler.LogError($"Failed to relaunch as admin: {elevateEx.Message}");
                        }
#else
                        LogHandler.LogError("Access denied when writing launch wrapper, and elevation is not supported on this OS.");
#endif
                        return;
                    }

                shortcut.TargetPath = launchWrapperPath;
                shortcut.TargetPath = launchWrapperPath;
                shortcut.Save();
                LogHandler.LogInfo($"Shortcut modified to use launch wrapper: {shortcutPath}");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void RemoveLaunchWrapper(string shortcutPath)
        {
            LogHandler.LogInfo($"Removing launch wrapper from shortcut: {shortcutPath}");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                LogHandler.LogError("Failed to get WScript.Shell COM type.");
                return;
            }
            var shellInstance = Activator.CreateInstance(shellType);
            if (shellInstance == null)
            {
                LogHandler.LogError("Failed to create WScript.Shell COM instance.");
                return;
            }
            dynamic shell = shellInstance;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string launchWrapperPath = shortcut.TargetPath;
            if (launchWrapperPath.EndsWith("launch_wrapper.bat", StringComparison.OrdinalIgnoreCase))
            {
                // read the launch wrapper to get the original target
                string originalTarget = "";
                try
                {
                    using (var reader = new StreamReader(launchWrapperPath))
                    {
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Use regex to extract the path between quotes for the first start command not containing 'orcapod'
                            if (line.StartsWith("start \"\" \"") && !line.Contains("orcapod"))
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(line, "start \\\"\\\" \\\"(.+?)\\\"");
                                if (match.Success)
                                {
                                    originalTarget = match.Groups[1].Value;
                                }
                                else
                                {
                                    // fallback to previous substring logic if regex fails
                                    int startIdx = line.IndexOf('"');
                                    int endIdx = line.LastIndexOf('"');
                                    if (startIdx >= 0 && endIdx > startIdx)
                                    {
                                        originalTarget = line.Substring(startIdx + 1, endIdx - startIdx - 1);
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHandler.LogError($"Failed to read launch wrapper: {ex.Message}");
                    return;
                }

                // Validate originalTarget before assignment
                if (!string.IsNullOrEmpty(originalTarget) &&
                    (File.Exists(originalTarget) || Directory.Exists(originalTarget)))
                {
                    try
                    {
                        shortcut.TargetPath = originalTarget;
                        shortcut.Save();
                        LogHandler.LogInfo($"Shortcut restored to original target: {originalTarget}");
                    }
                    catch (Exception ex)
                    {
                        LogHandler.LogError($"Failed to set shortcut target: {ex.Message}");
                        return;
                    }

                    // delete the launch wrapper file
                    try
                    {
                        File.Delete(launchWrapperPath);
                        LogHandler.LogInfo($"Launch wrapper file deleted: {launchWrapperPath}");
                    }
                    catch (Exception ex)
                    {
                        LogHandler.LogError($"Failed to delete launch wrapper file: {ex.Message}");
                    }
                }
                else
                {
                    LogHandler.LogError($"Invalid original target path: '{originalTarget}'");
                    LogHandler.LogWarning("Original target not found or invalid in launch wrapper. Skipping restoration.");
                }
            }
            else
            {
                LogHandler.LogInfo("Shortcut does not use launch wrapper. No action taken.");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void UninstallOnWindows()
        {
            foreach (var target in _programTargets)
            {
                try
                {
                    // get the path to the target's shortcut in both user and common Start Menu
                    string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                    string programsPath = Path.Combine(startMenuPath, "Programs");
                    string commonStartMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                    string commonProgramsPath = Path.Combine(commonStartMenuPath, "Programs");

                    var shortcutFiles = new List<string>();
                    if (Directory.Exists(programsPath))
                    {
                        shortcutFiles.AddRange(Directory.GetFiles(programsPath, "*.lnk", SearchOption.AllDirectories)
                            .Where(f => Path.GetFileNameWithoutExtension(f).Contains(target, StringComparison.OrdinalIgnoreCase)));
                    }
                    if (Directory.Exists(commonProgramsPath))
                    {
                        shortcutFiles.AddRange(Directory.GetFiles(commonProgramsPath, "*.lnk", SearchOption.AllDirectories)
                            .Where(f => Path.GetFileNameWithoutExtension(f).Contains(target, StringComparison.OrdinalIgnoreCase)));
                    }

                    string? shortcutPath = shortcutFiles.FirstOrDefault();

                    if (shortcutPath != null)
                    {
                        LogHandler.LogInfo($"Found shortcut for {target}: {shortcutPath}");
                        RemoveLaunchWrapper(shortcutPath);
                    }
                    else
                    {
                        throw new FileNotFoundException($"Shortcut for {target} not found in Start Menu.");
                    }
                }
                catch (Exception ex)
                {
                    LogHandler.LogError($"Failed to uninstall autostart for {target}: {ex.Message}");
                }
            }
        }

        private static void InstallOnLinux()
        {
            foreach (var target in _programTargets)
            {
                try
                {
                    // Get the user's home directory
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    // Search for the target's .desktop file in common locations
                    string[] searchPaths = new[]
                    {
                        Path.Combine(homeDir, ".local", "share", "applications"),
                        "/usr/share/applications",
                        "/usr/local/share/applications"
                    };

                    string? sourceDesktopFile = null;
                    foreach (var searchPath in searchPaths)
                    {
                        if (Directory.Exists(searchPath))
                        {
                            var desktopFiles = Directory.GetFiles(searchPath, "*.desktop", SearchOption.TopDirectoryOnly)
                                .Where(f => Path.GetFileNameWithoutExtension(f).Contains(target, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            sourceDesktopFile = desktopFiles.FirstOrDefault();
                            if (sourceDesktopFile != null)
                            {
                                LogHandler.LogInfo($"Found .desktop file for {target}: {sourceDesktopFile}");
                                break;
                            }
                        }
                    }

                    if (sourceDesktopFile == null)
                    {
                        throw new FileNotFoundException($".desktop file for {target} not found in standard locations.");
                    }

                    // Modify the .desktop file directly (or create a local copy if in system directory)
                    InjectLaunchWrapperLinux(sourceDesktopFile);
                }
                catch (Exception ex)
                {
                    LogHandler.LogError($"Failed to install autostart for {target}: {ex.Message}");
                }
            }
        }

        private static void InjectLaunchWrapperLinux(string desktopFile)
        {
            LogHandler.LogInfo($"Modifying .desktop file: {desktopFile}");

            // Get path for orcapod executable
            string orcapodPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            
            // If we got the .dll path, try to find the executable in the same directory
            if (orcapodPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(orcapodPath) ?? "";
                string executablePath = Path.Combine(directory, "orcapod");
                if (File.Exists(executablePath))
                {
                    orcapodPath = executablePath;
                }
            }

            // Read the original .desktop file
            var lines = File.ReadAllLines(desktopFile);
            string? originalExec = null;
            bool alreadyModified = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Exec="))
                {
                    originalExec = lines[i].Substring(5).Trim();

                    // Check if already modified
                    if (originalExec.Contains(orcapodPath))
                    {
                        alreadyModified = true;
                        LogHandler.LogInfo(".desktop file already contains orcapod wrapper. Skipping injection.");
                        break;
                    }
                }
            }

            if (alreadyModified)
            {
                return;
            }

            if (string.IsNullOrEmpty(originalExec))
            {
                LogHandler.LogError("Could not find Exec line in .desktop file.");
                return;
            }

            // Create a shell script wrapper
            string wrapperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "orcapod");
            if (!Directory.Exists(wrapperDir))
            {
                Directory.CreateDirectory(wrapperDir);
            }

            string wrapperScript = Path.Combine(wrapperDir, $"launch_wrapper_{Path.GetFileNameWithoutExtension(desktopFile)}.sh");

            try
            {
                using (var writer = new StreamWriter(wrapperScript))
                {
                    writer.WriteLine("#!/bin/bash");
                    writer.WriteLine($"{originalExec} &");
                    writer.WriteLine($"\"{orcapodPath}\" --background &");
                }

                // Make the script executable
                var chmodProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{wrapperScript}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                chmodProcess?.WaitForExit();

                LogHandler.LogInfo($"Created launch wrapper script: {wrapperScript}");
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Failed to create wrapper script: {ex.Message}");
                return;
            }

            // Determine target file location
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppsDir = Path.Combine(homeDir, ".local", "share", "applications");
            string targetDesktopFile;

            // If the desktop file is in a system directory, create a local copy
            if (desktopFile.StartsWith("/usr/"))
            {
                if (!Directory.Exists(localAppsDir))
                {
                    Directory.CreateDirectory(localAppsDir);
                }
                targetDesktopFile = Path.Combine(localAppsDir, Path.GetFileName(desktopFile));
                LogHandler.LogInfo($"System .desktop file detected. Creating local copy at: {targetDesktopFile}");
            }
            else
            {
                targetDesktopFile = desktopFile;
            }

            // Write modified .desktop file with wrapper script
            try
            {
                using (var writer = new StreamWriter(targetDesktopFile))
                {
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Exec="))
                        {
                            writer.WriteLine($"Exec={wrapperScript}");
                        }
                        else
                        {
                            writer.WriteLine(line);
                        }
                    }
                }

                LogHandler.LogInfo($"Modified .desktop file: {targetDesktopFile}");
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Failed to modify .desktop file: {ex.Message}");
            }
        }

        private static void UninstallOnLinux()
        {
            foreach (var target in _programTargets)
            {
                try
                {
                    // Get the user's home directory
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    // Search for the target's .desktop file in local applications
                    string localAppsDir = Path.Combine(homeDir, ".local", "share", "applications");
                    
                    if (!Directory.Exists(localAppsDir))
                    {
                        LogHandler.LogInfo($"Local applications directory does not exist. Nothing to uninstall for {target}.");
                        continue;
                    }

                    // Find the .desktop file
                    var desktopFiles = Directory.GetFiles(localAppsDir, "*.desktop", SearchOption.TopDirectoryOnly)
                        .Where(f => Path.GetFileNameWithoutExtension(f).Contains(target, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    string? desktopFile = desktopFiles.FirstOrDefault();

                    if (desktopFile == null)
                    {
                        LogHandler.LogInfo($"No modified .desktop file found for {target}.");
                        continue;
                    }

                    LogHandler.LogInfo($"Found .desktop file for {target}: {desktopFile}");
                    RemoveLaunchWrapperLinux(desktopFile);
                }
                catch (Exception ex)
                {
                    LogHandler.LogError($"Failed to uninstall autostart for {target}: {ex.Message}");
                }
            }
        }

        private static void RemoveLaunchWrapperLinux(string desktopFile)
        {
            LogHandler.LogInfo($"Removing launch wrapper from: {desktopFile}");

            try
            {
                // Read the .desktop file to find the wrapper script
                var lines = File.ReadAllLines(desktopFile);
                string? wrapperScript = null;
                string? originalExec = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Exec="))
                    {
                        wrapperScript = line.Substring(5).Trim();
                        break;
                    }
                }

                // If the Exec line points to a wrapper script, read it to get the original command
                if (!string.IsNullOrEmpty(wrapperScript) && wrapperScript.Contains("orcapod") && File.Exists(wrapperScript))
                {
                    try
                    {
                        var wrapperLines = File.ReadAllLines(wrapperScript);
                        foreach (var line in wrapperLines)
                        {
                            // Find the line that starts the original program (not orcapod)
                            if (!line.StartsWith("#") && !string.IsNullOrWhiteSpace(line) && !line.Contains("orcapod"))
                            {
                                // Remove trailing ' &' if present
                                originalExec = line.TrimEnd().TrimEnd('&').Trim();
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHandler.LogError($"Failed to read wrapper script: {ex.Message}");
                    }

                    // Delete the wrapper script
                    try
                    {
                        File.Delete(wrapperScript);
                        LogHandler.LogInfo($"Deleted wrapper script: {wrapperScript}");
                    }
                    catch (Exception ex)
                    {
                        LogHandler.LogError($"Failed to delete wrapper script: {ex.Message}");
                    }
                }

                // Delete the modified .desktop file from local applications
                // This will cause the system to fall back to the original .desktop file
                File.Delete(desktopFile);
                LogHandler.LogInfo($"Deleted modified .desktop file: {desktopFile}");
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Failed to remove launch wrapper: {ex.Message}");
            }
        }

        private static void TryFindProgramTargets()
        {
            var targets = SettingsHandler.GetSection("ProgramTargets")?.Get<string[]>();
            if (targets != null)
            {
                foreach (var target in targets)
                {
                    _programTargets.Add(target);
                    LogHandler.LogInfo($"Program target found for autostart: {target}");
                }
            }
            else
            {
                LogHandler.LogWarning("No program targets found in configuration.");
            }
        }
    }
}