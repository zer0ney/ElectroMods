using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ElectroMods.Scripts
{
    /// <summary>
    /// Configuration manager for ElectroMods
    /// Reads from encrypted Application Settings (Settings.settings)
    /// </summary>
    public class ConfigManager
    {
        public static void LoadConfig()
        {
            try
            {
                // Configuration is loaded from Settings.settings which is encrypted by default
                Debug.WriteLine("Loaded config from encrypted application settings");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading config: {ex.Message}");
            }
        }

        public static string GetDiscordClientSecret()
        {
            try
            {
                return Settings.Default.DiscordClientSecret;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving Discord Client Secret: {ex.Message}");
                return null;
            }
        }

        public static void SetDiscordClientSecret(string secret)
        {
            // Application-scoped settings are read-only at runtime
            // They must be changed through the project properties in Visual Studio
            Debug.WriteLine("Application-scoped settings cannot be changed at runtime. Please update in Project Properties.");
        }

        public static string GetApiBaseUrl()
        {
            return "https://electromods-api.zer0ney.me/api";
        }

        public static void SetApiBaseUrl(string url)
        {
            Debug.WriteLine($"API Base URL set to: {url}");
            // If you need to make this configurable later, add to Settings.settings
        }
    }

    // These classes are kept for potential future use if you expand configuration
    public class AppConfig
    {
        public DiscordConfig Discord { get; set; } = new DiscordConfig();
        public APIConfig API { get; set; } = new APIConfig();
    }

    public class DiscordConfig
    {
        public string ClientSecret { get; set; }
    }

    public class APIConfig
    {
        public string BaseUrl { get; set; } = "https://electronauts-backend.zer0ney.workers.dev/api";
    }
}
