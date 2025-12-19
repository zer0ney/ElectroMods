using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElectroMods.Scripts
{
    public class SettingsManager
    {
        private const string DiscordTokenKey = "DiscordAccessToken";

        public SettingsManager()
        {

        }
        
        public static void SaveAuthToken(string token)
        {
            SaveSetting(DiscordTokenKey, token);
        }

        public static string LoadAuthToken()
        {
            return LoadSetting(DiscordTokenKey);
        }

        public static void ClearAuthToken()
        {
            SaveSetting(DiscordTokenKey, "");
        }

        public static bool IsUserLoggedIn()
        {
            var token = LoadAuthToken();
            return !string.IsNullOrEmpty(token);
        }

        public static void SaveSetting(string settingName, string settingValue)
        {
            try
            {
                // Use the Settings indexer directly - it handles User-scoped settings
                Settings.Default[settingName] = settingValue ?? "";
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving setting {settingName}: {ex.Message}");
            }
        }

        public static string LoadSetting(string settingName)
        {
            try
            {
                var value = Settings.Default[settingName];
                return value?.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading setting {settingName}: {ex.Message}");
                return null;
            }
        }

        public static void SaveAll()
        {
            try
            {
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving all settings: {ex.Message}");
            }
        }
    }
}
