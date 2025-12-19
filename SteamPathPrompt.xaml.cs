using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using ElectroMods.Scripts;
using Path = System.IO.Path;

namespace ElectroMods
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class SteamPathPrompt : Window
    {
        bool isNormalClose;
        public SteamPathPrompt()
        {
            InitializeComponent();
        }
        
        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = Environment.CurrentDirectory;
            dialog.IsFolderPicker = true;
            
            CommonFileDialogResult dialogResult = dialog.ShowDialog();
            
            if (dialogResult == CommonFileDialogResult.Ok)
            {
                textBox.Text = dialog.FileName;
                checkFolder(dialog.FileName);
            }
        }
        
        private void checkFolder(string folderLoc)
        {
            if (string.IsNullOrEmpty(folderLoc))
            {
                return;
            }
            
            string exePath = Path.Combine(folderLoc, "Electronauts.exe");
            
            if (File.Exists(exePath))
            {
                SettingsManager.SaveSetting("ElectronautsSteamPath", folderLoc);
                isNormalClose = true;
                this.DialogResult = true;
                this.Close();
            } 
            else
            {
                MessageBox.Show("Unable to find required files at: " + folderLoc + "\n\nPlease double check the folder and try again.");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isNormalClose)
            {
                var result = MessageBox.Show("You still need to select your Electronauts path, are you sure you want to close? You can select the path on next start.", "Warning", MessageBoxButton.YesNo);
                
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
