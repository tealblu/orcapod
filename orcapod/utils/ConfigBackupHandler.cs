using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Orcapod.Utils
{
    public class ConfigBackupHandler
    {
        private readonly Microsoft.Extensions.Logging.ILogger<OrcaPod.Service.MainService> _logger;
        private Dictionary<string, string> _mappings = new Dictionary<string, string>();

        public ConfigBackupHandler(Microsoft.Extensions.Logging.ILogger<OrcaPod.Service.MainService> logger)
        {
            _logger = logger;
            _mappings = LoadMappings();
        }

        public Dictionary<string, string> LoadMappings()
        {
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

        public void BackupConfig(string sourcePath)
        {
            if (_mappings.TryGetValue(sourcePath, out var destinationPath))
            {
                try
                {
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                    _logger.LogInformation($"Backed up '{sourcePath}' to '{destinationPath}'");
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

        public bool CheckIfNewerBackupExists(string sourcePath)
        {
            if (_mappings.TryGetValue(sourcePath, out var destinationPath))
            {
                if (File.Exists(destinationPath))
                {
                    var sourceInfo = new FileInfo(sourcePath);
                    var destInfo = new FileInfo(destinationPath);
                    return destInfo.LastWriteTime > sourceInfo.LastWriteTime;
                }
            }
            return false;
        }

        public void RestoreConfig(string sourcePath)
        {
            if (_mappings.TryGetValue(sourcePath, out var destinationPath))
            {
                try
                {
                    File.Copy(destinationPath, sourcePath, overwrite: true);
                    _logger.LogInformation($"Restored '{sourcePath}' from '{destinationPath}'");
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

        public void LoadUpdatedConfigs()
        {
            _logger.LogInformation("Checking for updated configuration files to restore.");
            foreach (var mapping in _mappings)
            {
                var sourcePath = mapping.Key;
                if (CheckIfNewerBackupExists(sourcePath))
                {
                    _logger.LogInformation($"Newer backup found for '{sourcePath}', restoring.");
                    RestoreConfig(sourcePath);
                }
            }
        }

        public void Dispose()
        {
            // Cleanup if necessary
        }
    }
}