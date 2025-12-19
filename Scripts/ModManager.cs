using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace ElectroMods.Scripts
{
    internal class ModManager
    {
        /// <summary>
        /// Gets the directory where the executable is located (works with single-file publish)
        /// </summary>
        private static string GetExecutableDirectory()
        {
            // For single-file executables, use the process path
            string executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
        }

        /// <summary>
        /// Checks if BepInEx is installed in the Electronauts directory
        /// </summary>
        public static bool IsBepInExInstalled()
        {
            string steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                return false;

            // Check for BepInEx core files
            string bepInExPath = Path.Combine(steamPath, "BepInEx");
            string winHttpPath = Path.Combine(steamPath, "winhttp.dll");
            string doorstopConfigPath = Path.Combine(steamPath, "doorstop_config.ini");

            return Directory.Exists(bepInExPath) && 
                   (File.Exists(winHttpPath) || File.Exists(doorstopConfigPath));
        }

        /// <summary>
        /// Installs BepInEx to the Electronauts directory automatically
        /// </summary>
        public static bool InstallBepInEx()
        {
            try
            {
                string steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                {
                    MessageBox.Show("Electronauts path not set or invalid. Please verify your installation path.");
                    return false;
                }

                // Look for bundled BepInEx using executable directory
                string executableDir = GetExecutableDirectory();
                string bepInExZipPath = Path.Combine(executableDir, "BepInEx", "BepInEx_x64.zip");
                
                if (!File.Exists(bepInExZipPath))
                {
                    MessageBox.Show($"BepInEx installation files not found at:\n{bepInExZipPath}\n\nPlease reinstall ElectroMods.");
                    return false;
                }

                StatusBar.Update("Installing BepInEx...");

                // Extract BepInEx to the game directory
                ZipFile.ExtractToDirectory(bepInExZipPath, steamPath, true);

                StatusBar.Update("BepInEx installed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to install BepInEx: {ex.Message}\n\nPlease ensure the game is not running and try again.");
                return false;
            }
        }

        /// <summary>
        /// Gets the BepInEx plugins directory path
        /// </summary>
        public static string GetBepInExPluginsPath()
        {
            string steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                return null;

            return Path.Combine(steamPath, "BepInEx", "plugins");
        }

        /// <summary>
        /// Ensures the Mods folder exists in Documents for custom songs
        /// </summary>
        public static bool EnsureModsFolderExists()
        {
            try
            {
                string steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                    return false;

                string internalModsFolder = Path.Combine(steamPath, "Electronauts_Data/StreamingAssets/DefaultMods");
                string externalModsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronauts/Mods");

                // Create external mods folder if it doesn't exist
                if (!Directory.Exists(externalModsFolder))
                {
                    Directory.CreateDirectory(externalModsFolder);
                    
                    // Copy internal mods folder to documents if it exists
                    if (Directory.Exists(internalModsFolder))
                    {
                        Utilities.CopyDirectory(internalModsFolder, externalModsFolder, true);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the ElectroMods patcher plugin is installed
        /// </summary>
        public static bool IsElectroModsPluginInstalled()
        {
            string pluginsPath = GetBepInExPluginsPath();
            if (string.IsNullOrEmpty(pluginsPath) || !Directory.Exists(pluginsPath))
                return false;

            // Look for the ElectroMods patcher DLL
            string patcherPath = Path.Combine(pluginsPath, "ElectroModsPatcher.dll");
            return File.Exists(patcherPath);
        }

        /// <summary>
        /// Installs the ElectroMods patcher plugin to BepInEx
        /// </summary>
        public static bool InstallElectroModsPlugin()
        {
            try
            {
                string pluginsPath = GetBepInExPluginsPath();
                if (string.IsNullOrEmpty(pluginsPath))
                {
                    MessageBox.Show("BepInEx plugins folder not found. Please ensure BepInEx is installed correctly.");
                    return false;
                }

                // Create plugins directory if it doesn't exist
                Directory.CreateDirectory(pluginsPath);

                // Copy the patcher DLL from the executable directory
                string executableDir = GetExecutableDirectory();
                string sourcePath = Path.Combine(executableDir, "Plugins", "ElectroModsPatcher.dll");
                string destPath = Path.Combine(pluginsPath, "ElectroModsPatcher.dll");

                if (!File.Exists(sourcePath))
                {
                    MessageBox.Show($"ElectroModsPatcher.dll not found at:\n{sourcePath}");
                    return false;
                }

                File.Copy(sourcePath, destPath, true);
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error installing plugin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs everything needed: BepInEx and the plugin
        /// </summary>
        public static bool SetupModding()
        {
            // Install BepInEx if not present
            if (!IsBepInExInstalled())
            {
                if (!InstallBepInEx())
                    return false;
            }

            // Install plugin if not present
            if (!IsElectroModsPluginInstalled())
            {
                if (!InstallElectroModsPlugin())
                    return false;
            }

            // Ensure mods folder exists
            EnsureModsFolderExists();

            return true;
        }
    }
}
