using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Orcapod.Utils
{
    public class ConfigBackupHandler
    {
        private readonly Microsoft.Extensions.Logging.ILogger<OrcaPod.Service.MainService> _logger;

        public ConfigBackupHandler(Microsoft.Extensions.Logging.ILogger<OrcaPod.Service.MainService> logger)
        {
            _logger = logger;
            LoadMappings();
        }

        public Dictionary<string, string> LoadMappings()
        {
            _logger.LogInformation("Loading backup mappings");
            var mappings = new Dictionary<string, string>();
            var section = SettingsHandler.GetSection("Mappings");

            if (section != null)
            {
                foreach (var child in section.GetChildren())
                {
                    var source = child.Key;
                    var destination = child.Value;
                    if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(destination))
                    {
                        mappings[source] = destination;
                        _logger.LogInformation($"Mapping loaded: {source} -> {destination}");
                    }
                }
            }
            return mappings;
        }
    }
}