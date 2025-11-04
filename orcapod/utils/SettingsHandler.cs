using System;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace OrcaPod.Utils
{
    public static class SettingsHandler
    {
        private static IConfigurationRoot _configuration;
        private static string jsonFileName = "settings.json";
        private static string jsonFilePath = Path.Combine(AppContext.BaseDirectory, jsonFileName);

        static SettingsHandler()
        {
            // If JSON file does not exist, throw an exception
            if (!File.Exists(jsonFilePath))
            {
                throw new FileNotFoundException($"Configuration file '{jsonFileName}' not found.");
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(jsonFileName, optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        public static string Get(string key)
        {
            return _configuration[key] ?? string.Empty;
        }

        public static T Get<T>(string key)
        {
            return _configuration.GetValue<T>(key, default(T)!) ?? default(T)!;
        }

        public static IConfigurationSection? GetSection(string sectionName)
        {
            return _configuration.GetSection(sectionName);
        }
    }
}