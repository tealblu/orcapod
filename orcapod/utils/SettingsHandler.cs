using System;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Orcapod.Utils
{
    public static class SettingsHandler
    {
        private static IConfigurationRoot _configuration;
        private static string iniFileName = "settings.ini";
        private static string iniFilePath = Path.Combine(AppContext.BaseDirectory, iniFileName);

        static SettingsHandler()
        {
            // If INI file does not exist, throw an exception
            if (!File.Exists(iniFilePath))
            {
                throw new FileNotFoundException($"Configuration file '{iniFileName}' not found.");
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddIniFile(iniFilePath, optional: false, reloadOnChange: true);

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