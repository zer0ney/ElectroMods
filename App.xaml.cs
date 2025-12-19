using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ElectroMods.Scripts;

namespace ElectroMods
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Create and show the main window first
            MainWindow window = new MainWindow();
            Application.Current.MainWindow = window;
            window.Show();
            
            // Check if steam path is set
            string steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
            
            if (string.IsNullOrEmpty(steamPath))
            {
                SteamPathPrompt prompt = new SteamPathPrompt();
                prompt.Owner = window; // Set MainWindow as owner so dialog centers on it
                bool? result = prompt.ShowDialog();
                
                // Check if the user successfully selected a path
                steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
                
                if (string.IsNullOrEmpty(steamPath))
                {
                    MessageBox.Show("Electronauts Steam path is required to run this application.");
                    this.Shutdown();
                    return;
                }
                
                // Refresh the main window after path is set
                window.RefreshAfterPathSet();
            }
        }
    }
}
