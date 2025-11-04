using System;
using System.IO;

namespace Orcapod.Utils
{
    public static class LogHandler
    {
        private static string? _logFilePath;
        private static bool _printToConsole;

        public static void Initialize(string logFilePath)
        {
            _logFilePath = logFilePath;
            // Detect if running in VS Code by checking environment variable
            _printToConsole = Environment.GetEnvironmentVariable("TERM_PROGRAM") == "vscode";
        }

        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public static void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        public static void LogError(string message)
        {
            Log("ERROR", message);
        }

        private static void Log(string level, string message)
        {
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            try
            {
                File.AppendAllText(_logFilePath ?? "default.log", logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write log: {ex.Message}");
            }

            if (_printToConsole)
            {
                if (level == "ERROR")
                    Console.Error.WriteLine(logEntry);
                else
                    Console.WriteLine(logEntry);
            }
        }
    }
}