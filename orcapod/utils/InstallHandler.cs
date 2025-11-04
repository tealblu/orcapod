using System;
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
        }

        private static void InjectLaunchWrapper(string shortcutPath)
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
            catch (UnauthorizedAccessException ex)
            {
#if WINDOWS
                LogHandler.LogError($"Access denied when writing launch wrapper. Attempting to relaunch with admin rights: {ex.Message}");
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
#endif
                return;
            }

            shortcut.TargetPath = launchWrapperPath;
            shortcut.Save();
            LogHandler.LogInfo($"Shortcut updated to use launch wrapper: {launchWrapperPath}");
        }

        private static void RemoveLaunchWrapper(string shortcutPath)
        {
            // Logic to remove the launch wrapper from the shortcut using dynamic COM interop
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
            throw new NotImplementedException("Linux installation not implemented yet.");
        }

        private static void UninstallOnLinux()
        {
            throw new NotImplementedException("Linux uninstallation not implemented yet.");
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