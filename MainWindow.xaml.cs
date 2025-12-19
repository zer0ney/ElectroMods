using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ElectroMods.Scripts;
using System.IO;

namespace ElectroMods
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //refs
        public static TextBlock statusBar = null!;
        private string? selectedSongFilePath;
        
        // Pagination state
        private int currentSongsPage = 1;
        private int currentSearchPage = 1;
        private int songsPageSize = 50;
        private int searchPageSize = 50;
        private int totalSongsPages = 1;
        private int totalSearchPages = 1;
        private int totalSongs = 0;
        private int totalSearchResults = 0;
        
        // Search parameters cache
        private string? lastSearchName = null;
        private string? lastSearchArtist = null;
        private string? lastSearchGenre = null;
        private string? lastSearchBPM = null;
        private string? lastSearchAuthor = null;
        
        //events
        public MainWindow()
        {
            //initialise main window and set references
            InitializeComponent();
            statusBar = statusBarText;

            try
            {
                string? steamPath = SettingsManager.LoadSetting("ElectronautsSteamPath");
                if (!string.IsNullOrEmpty(steamPath))
                {
                    InitializeApplication();
                }
                else
                {
                    StatusBar.Update("Please select your Electronauts installation path...");
                }
            }
            catch (Exception)
            {
                StatusBar.Update("Error initializing application");
            }
        }

        public void RefreshAfterPathSet()
        {
            try
            {
                InitializeApplication();
            }
            catch (Exception)
            {
                StatusBar.Update("Error initializing application");
            }
        }

        private void InitializeApplication()
        {
            // Load configuration
            ConfigManager.LoadConfig();

            // Load stored auth token if available
            var token = SettingsManager.LoadAuthToken();
            if (!string.IsNullOrEmpty(token))
            {
                ApiClient.SetDiscordAccessToken(token);
            }

            // Check if modding is set up (BepInEx + plugin)
            if (!ModManager.IsBepInExInstalled() || !ModManager.IsElectroModsPluginInstalled())
            {
                var result = MessageBox.Show(
                    "ElectroMods needs to install modding support to enable custom songs in Electronauts.\n\n" +
                    "This will:\n" +
                    "• Install BepInEx (modding framework)\n" +
                    "• Install ElectroMods plugin\n" +
                    "• Create the Mods folder\n\n" +
                    "This is safe and can be easily uninstalled.\n\n" +
                    "Continue with installation?",
                    "Setup Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (ModManager.SetupModding())
                    {
                        MessageBox.Show(
                            "Modding support installed successfully!\n\n" +
                            "Please launch Electronauts once to initialize the mod, then you're ready to download songs!",
                            "Installation Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        StatusBar.Update("Modding support installed!");
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to install modding support.\n\n" +
                            "You can still browse and download songs, but they won't appear in-game until modding is set up.",
                            "Installation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        StatusBar.Update("Modding installation failed");
                    }
                }
                else
                {
                    StatusBar.Update("Modding not installed - browse-only mode");
                }
            }
            else
            {
                StatusBar.Update("Modding support ready!");
            }

            StatusBar.Update("Loading songs...");

            //load components asynchronously to prevent UI freeze
            _ = LoadSongsAsync();
            
            // Update the path label if window is already rendered
            testLabel.Content = SettingsManager.LoadSetting("ElectronautsSteamPath");
        }

        private async Task LoadSongsAsync()
        {
            try
            {
                StatusBar.Update($"Loading songs (page {currentSongsPage})...");
                
                var paginatedResponse = await ApiClient.GetAllSongsAsync(currentSongsPage, songsPageSize);
                
                if (paginatedResponse != null)
                {
                    var localSongs = SongRetriever.GetLocalSongData();
                    
                    foreach (var song in paginatedResponse.Songs)
                    {
                        var localMatch = localSongs.FirstOrDefault(ls => 
                            (!string.IsNullOrEmpty(song.Id) && !string.IsNullOrEmpty(ls.Id) && ls.Id == song.Id) ||
                            (ls.Name == song.Name));

                        if (localMatch != null)
                        {
                            bool installedThroughManager = localMatch.UpdatedAt > 0;
                            
                            if (!installedThroughManager)
                            {
                                song.ButtonText = "Local";
                                song.DownloadEnabled = false;
                            }
                            else
                            {
                                long serverUpdatedAtSeconds = song.UpdatedAt / 1000;
                                
                                if (serverUpdatedAtSeconds > localMatch.UpdatedAt)
                                {
                                    song.ButtonText = "Update";
                                    song.DownloadEnabled = true;
                                }
                                else
                                {
                                    song.ButtonText = "Remove";
                                    song.DownloadEnabled = true;
                                }
                            }
                        }
                        else
                        {
                            song.ButtonText = "Download";
                            song.DownloadEnabled = true;
                        }
                    }
                    
                    SongDataGrid.ItemsSource = paginatedResponse.Songs;
                    totalSongsPages = paginatedResponse.TotalPages;
                    totalSongs = paginatedResponse.Total;
                    
                    UpdateSongsPaginationUI();
                    StatusBar.Update("Loaded!");
                }
            }
            catch (Exception ex)
            {
                StatusBar.Update("Error loading songs: " + ex.Message);
            }
        }

        private void UpdateSongsPaginationUI()
        {
            SongsPageInfo.Text = $"Page {currentSongsPage} of {totalSongsPages} ({totalSongs} songs)";
            SongsPreviousButton.IsEnabled = currentSongsPage > 1;
            SongsNextButton.IsEnabled = currentSongsPage < totalSongsPages;
        }

        private async void SongsPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSongsPage > 1)
            {
                currentSongsPage--;
                await LoadSongsAsync();
            }
        }

        private async void SongsNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSongsPage < totalSongsPages)
            {
                currentSongsPage++;
                await LoadSongsAsync();
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            testLabel.Content = SettingsManager.LoadSetting("ElectronautsSteamPath");
            UpdateAuthUI();
        }

        private void UpdateAuthUI()
        {
            if (SettingsManager.IsUserLoggedIn())
            {
                LoggedOutPanel.Visibility = Visibility.Collapsed;
                LoggedInPanel.Visibility = Visibility.Visible;
                
                var username = SettingsManager.LoadSetting("DiscordUsername");
                
                if (!string.IsNullOrEmpty(username))
                {
                    UserInfoText.Text = $"Logged in as: {username}";
                }
                else
                {
                    UserInfoText.Text = "Logged in";
                }
                
                // Update upload UI when logged in
                UpdateUploadUI();
            }
            else
            {
                LoggedOutPanel.Visibility = Visibility.Visible;
                LoggedInPanel.Visibility = Visibility.Collapsed;
                
                // Update upload UI when logged out
                UpdateUploadUI();
            }
        }

        private void UpdateUploadUI()
        {
            if (SettingsManager.IsUserLoggedIn())
            {
                UploadNotLoggedInPanel.Visibility = Visibility.Collapsed;
                UploadLoggedInPanel.Visibility = Visibility.Visible;
                _ = LoadUserSongsAsync();
            }
            else
            {
                UploadNotLoggedInPanel.Visibility = Visibility.Visible;
                UploadLoggedInPanel.Visibility = Visibility.Collapsed;
                NoSongsPanel.Visibility = Visibility.Visible;
                UserSongsDataGrid.Visibility = Visibility.Collapsed;
                selectedSongFilePath = null;
                SelectedFileText.Text = "No file selected";
                UploadSongButton.IsEnabled = false;
            }
        }

        private async Task LoadUserSongsAsync()
        {
            try
            {
                var (success, songs, message) = await ApiClient.GetUserSongsAsync();

                if (success && songs != null && songs.Count > 0)
                {
                    UserSongsDataGrid.ItemsSource = songs;
                    NoSongsPanel.Visibility = Visibility.Collapsed;
                    UserSongsDataGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    NoSongsPanel.Visibility = Visibility.Visible;
                    UserSongsDataGrid.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception)
            {
                NoSongsPanel.Visibility = Visibility.Visible;
                UserSongsDataGrid.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Client secret is stored in encrypted app settings
                var clientSecret = ConfigManager.GetDiscordClientSecret();
                if (string.IsNullOrEmpty(clientSecret))
                {
                    MessageBox.Show("Discord Client Secret is not configured in application settings.");
                    StatusBar.Update("Client Secret missing");
                    return;
                }

                StatusBar.Update("Opening Discord login...");
                
                // Generate auth URL and open in browser
                string authUrl = AuthManager.GetDiscordAuthUrl();
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                // Start listening for callback
                StatusBar.Update("Waiting for authorization...");
                string authCode = await AuthManager.StartCallbackListenerAsync();

                if (string.IsNullOrEmpty(authCode))
                {
                    MessageBox.Show("Authorization failed or was cancelled.");
                    StatusBar.Update("Authorization cancelled");
                    return;
                }

                var (accessToken, user) = await AuthManager.ExchangeCodeForTokenAsync(authCode, clientSecret);

                if (string.IsNullOrEmpty(accessToken) || user == null)
                {
                    MessageBox.Show("Failed to authenticate with Discord.");
                    StatusBar.Update("Authentication failed");
                    return;
                }

                // Save token and user info
                SettingsManager.SaveAuthToken(accessToken);
                SettingsManager.SaveSetting("DiscordUsername", user.DisplayName);
                SettingsManager.SaveSetting("DiscordUserId", user.Id);
                
                // Update API client with token
                ApiClient.SetDiscordAccessToken(accessToken);

                UpdateAuthUI();
                MessageBox.Show($"Successfully logged in as {user.DisplayName}!");
                StatusBar.Update($"Logged in as {user.DisplayName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during login: {ex.Message}");
                StatusBar.Update("Login error");
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SettingsManager.ClearAuthToken();
                SettingsManager.SaveSetting("DiscordUsername", "");
                SettingsManager.SaveSetting("DiscordUserId", "");
                ApiClient.SetDiscordAccessToken(null);

                UpdateAuthUI();
                MessageBox.Show("You have been logged out.");
                StatusBar.Update("Logged out");
                
                UpdateUploadUI();
            }
            catch (Exception)
            {
                StatusBar.Update("Logout error");
            }
        }

        private void DeleteAllDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete all saved data?\n\n" +
                    "This will remove:\n" +
                    "• Discord authentication\n" +
                    "• Saved paths\n" +
                    "• All application preferences\n\n" +
                    "Your downloaded songs will NOT be affected.\n\n" +
                    "This action cannot be undone.",
                    "Confirm Delete All Data",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                StatusBar.Update("Deleting all data...");

                // Clear authentication
                SettingsManager.ClearAuthToken();
                SettingsManager.SaveSetting("DiscordUsername", "");
                SettingsManager.SaveSetting("DiscordUserId", "");
                ApiClient.SetDiscordAccessToken(null);

                // Clear all other settings
                SettingsManager.SaveSetting("ElectronautsSteamPath", "");
                
                // Reset all settings to default
                Settings.Default.Reset();
                Settings.Default.Save();

                // Update UI
                UpdateAuthUI();
                UpdateUploadUI();
                testLabel.Content = "Loading...";

                MessageBox.Show("All data has been deleted successfully.\n\nThe application will now close. Please restart to reconfigure.", 
                    "Data Deleted", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);

                StatusBar.Update("All data deleted");

                // Close the application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting data: {ex.Message}");
                StatusBar.Update("Delete error");
            }
        }

        // methods
        public void SongDataGridLoad()
        {
            try
            {
                List<Classes.Song> localSongs = SongRetriever.GetAllSongs();
                SongDataGrid.ItemsSource = localSongs;
            }
            catch (Exception ex)
            {
                StatusBar.Update("Error loading songs: " + ex.Message);
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button? downloadButton = sender as Button;
                Classes.Song? songToDownload = ((FrameworkElement)sender).DataContext as Classes.Song;

                if (songToDownload == null || string.IsNullOrEmpty(songToDownload.Id) || downloadButton == null)
                {
                    MessageBox.Show("Error: Song information is missing");
                    return;
                }

                bool isUpdate = songToDownload.ButtonText == "Update";
                bool isRemove = songToDownload.ButtonText == "Remove";
                
                if (isRemove)
                {
                    var result = MessageBox.Show($"Are you sure you want to remove '{songToDownload.Name}'?", 
                                                "Confirm Removal", 
                                                MessageBoxButton.YesNo, 
                                                MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                    
                    downloadButton.IsEnabled = false;
                    downloadButton.Content = "Removing...";
                    StatusBar.Update($"Removing {songToDownload.Name}...");
                    
                    bool success = SongDownloader.RemoveSong(songToDownload);
                    
                    if (success)
                    {
                        MessageBox.Show($"Successfully removed {songToDownload.Name}!");
                        StatusBar.Update("Removal complete! Refreshing...");
                        
                        await Task.Delay(500);
                        
                        await LoadSongsAsync();
                        
                        if (SearchResultsDataGrid.ItemsSource != null)
                        {
                            await RefreshSearchResults();
                        }
                        
                        StatusBar.Update("Loaded!");
                    }
                    else
                    {
                        MessageBox.Show($"Failed to remove {songToDownload.Name}");
                        StatusBar.Update("Removal failed");
                        downloadButton.IsEnabled = true;
                        downloadButton.Content = "Remove";
                    }
                    
                    return;
                }
                
                string actionText = isUpdate ? "Updating" : "Downloading";
                string originalButtonText = songToDownload.ButtonText;
                
                downloadButton.IsEnabled = false;
                downloadButton.Content = $"{actionText}...";
                StatusBar.Update($"{actionText} {songToDownload.Name}...");

                bool downloadSuccess = await SongDownloader.DownloadSongAsync(songToDownload);
                
                if (downloadSuccess)
                {
                    string actionMessage = isUpdate ? "updated" : "installed";
                    MessageBox.Show($"Successfully {actionMessage} {songToDownload.Name}!");
                    StatusBar.Update("Download complete! Refreshing...");
                    
                    await Task.Delay(500);
                    
                    await LoadSongsAsync();
                    
                    if (SearchResultsDataGrid.ItemsSource != null)
                    {
                        await RefreshSearchResults();
                    }
                    
                    StatusBar.Update("Loaded!");
                }
                else
                {
                    MessageBox.Show($"Failed to download {songToDownload.Name}");
                    StatusBar.Update("Download failed");
                    downloadButton.IsEnabled = true;
                    downloadButton.Content = originalButtonText;
                }
            }
            catch (Exception)
            {
                StatusBar.Update("Error during operation");
            }
        }

        private void SelectSongButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
                    Title = "Select Song ZIP File"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedSongFilePath = openFileDialog.FileName;
                    SelectedFileText.Text = System.IO.Path.GetFileName(selectedSongFilePath);
                    UploadSongButton.IsEnabled = true;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error selecting file");
            }
        }

        private async void UploadSongButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(selectedSongFilePath))
                {
                    MessageBox.Show("Please select a ZIP file to upload.");
                    return;
                }

                if (!System.IO.File.Exists(selectedSongFilePath))
                {
                    MessageBox.Show("The selected file no longer exists.");
                    selectedSongFilePath = null;
                    SelectedFileText.Text = "No file selected";
                    UploadSongButton.IsEnabled = false;
                    return;
                }

                SelectSongButton.IsEnabled = false;
                UploadSongButton.IsEnabled = false;

                var progress = new Progress<long>(bytes =>
                {
                    var fileInfo = new System.IO.FileInfo(selectedSongFilePath);
                    long totalBytes = fileInfo.Length;
                    
                    if (totalBytes > 0)
                    {
                        int percentage = (int)((bytes * 100) / totalBytes);
                        StatusBar.Update($"Uploading... {percentage}%");
                    }
                });

                StatusBar.Update("Uploading... 0%");
                var (success, message) = await ApiClient.UploadSongWithProgressAsync(selectedSongFilePath, progress);

                if (success)
                {
                    MessageBox.Show($"{message}");
                    StatusBar.Update("Upload complete!");
                    
                    selectedSongFilePath = null;
                    SelectedFileText.Text = "No file selected";
                    
                    await LoadUserSongsAsync();
                }
                else
                {
                    MessageBox.Show($"Upload failed: {message}");
                    StatusBar.Update("Upload failed");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error uploading song: {ex.Message}");
                StatusBar.Update("Upload error");
            }
            finally
            {
                SelectSongButton.IsEnabled = true;
                if (!string.IsNullOrEmpty(selectedSongFilePath))
                {
                    UploadSongButton.IsEnabled = true;
                }
            }
        }

        private async void UpdateSongButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button? updateButton = sender as Button;
                Classes.Song? song = ((FrameworkElement)sender).DataContext as Classes.Song;

                if (song == null || string.IsNullOrEmpty(song.Id))
                {
                    MessageBox.Show("Error: Song information is missing");
                    return;
                }

                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
                    Title = $"Select Updated ZIP File for {song.Name}"
                };

                if (openFileDialog.ShowDialog() != true)
                {
                    return;
                }

                if (updateButton != null)
                {
                    updateButton.IsEnabled = false;
                }
                
                StatusBar.Update($"Updating {song.Name}... 0%");

                var progress = new Progress<long>(bytes =>
                {
                    var fileInfo = new System.IO.FileInfo(openFileDialog.FileName);
                    long totalBytes = fileInfo.Length;
                    
                    if (totalBytes > 0)
                    {
                        int percentage = (int)((bytes * 100) / totalBytes);
                        StatusBar.Update($"Updating {song.Name}... {percentage}%");
                    }
                });

                var (success, message) = await ApiClient.UpdateSongWithProgressAsync(song.Id, openFileDialog.FileName, progress);

                if (success)
                {
                    MessageBox.Show($"{message}");
                    StatusBar.Update("Update complete!");
                    await LoadUserSongsAsync();
                }
                else
                {
                    MessageBox.Show($"Update failed: {message}");
                    StatusBar.Update("Update failed");
                }
            }
            catch (Exception)
            {
                StatusBar.Update("Update error");
            }
            finally
            {
                Button? updateButton = sender as Button;
                if (updateButton != null)
                {
                    updateButton.IsEnabled = true;
                }
            }
        }

        private async void DeleteSongButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button? deleteButton = sender as Button;
                Classes.Song? song = ((FrameworkElement)sender).DataContext as Classes.Song;

                if (song == null || string.IsNullOrEmpty(song.Id))
                {
                    MessageBox.Show("Error: Song information is missing");
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to delete '{song.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                if (deleteButton != null)
                {
                    deleteButton.IsEnabled = false;
                }
                
                StatusBar.Update($"Deleting {song.Name}...");

                var (success, message) = await ApiClient.DeleteSongAsync(song.Id);

                if (success)
                {
                    MessageBox.Show($"{message}");
                    StatusBar.Update("Delete complete!");
                    await LoadUserSongsAsync();
                }
                else
                {
                    MessageBox.Show($"Delete failed: {message}");
                    StatusBar.Update("Delete failed");
                }
            }
            catch (Exception)
            {
                StatusBar.Update("Delete error");
            }
            finally
            {
                Button? deleteButton = sender as Button;
                if (deleteButton != null)
                {
                    deleteButton.IsEnabled = true;
                }
            }
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusBar.Update("Refreshing...");
                
                // Reset Songs tab to first page and refresh
                currentSongsPage = 1;
                await LoadSongsAsync();
                
                // If user is logged in, refresh their uploaded songs
                if (SettingsManager.IsUserLoggedIn())
                {
                    await LoadUserSongsAsync();
                }
                
                // If there are search results, re-run the search to refresh them
                if (SearchResultsDataGrid.ItemsSource != null)
                {
                    await RefreshSearchResults();
                }
                
                StatusBar.Update("Refresh complete!");
            }
            catch (Exception)
            {
                StatusBar.Update("Refresh failed");
            }
        }

        private async Task RefreshSearchResults()
        {
            try
            {
                // Only refresh if there are active search parameters
                if (lastSearchName == null && lastSearchArtist == null && lastSearchGenre == null && 
                    lastSearchBPM == null && lastSearchAuthor == null)
                {
                    return;
                }

                // Re-run the search with current page
                await PerformSearchAsync();
            }
            catch (Exception)
            {
                // Silently continue
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Cache search parameters
                lastSearchName = SearchNameTextBox.Text?.Trim();
                lastSearchArtist = SearchArtistTextBox.Text?.Trim();
                lastSearchGenre = SearchGenreTextBox.Text?.Trim();
                lastSearchBPM = SearchBPMTextBox.Text?.Trim();
                lastSearchAuthor = SearchAuthorTextBox.Text?.Trim();
                
                // Reset to first page on new search
                currentSearchPage = 1;
                
                await PerformSearchAsync();
            }
            catch (Exception)
            {
                StatusBar.Update("Error searching songs");
            }
        }

        private async Task PerformSearchAsync()
        {
            try
            {
                StatusBar.Update($"Searching (page {currentSearchPage})...");

                var paginatedResponse = await ApiClient.SearchSongsAsync(
                    currentSearchPage, 
                    searchPageSize, 
                    lastSearchName, 
                    lastSearchArtist, 
                    lastSearchGenre, 
                    lastSearchBPM, 
                    lastSearchAuthor);

                if (paginatedResponse != null && paginatedResponse.Songs != null && paginatedResponse.Songs.Count > 0)
                {
                    var localSongs = SongRetriever.GetLocalSongData();
                    
                    foreach (var searchSong in paginatedResponse.Songs)
                    {
                        var localMatch = localSongs.FirstOrDefault(ls => 
                            (!string.IsNullOrEmpty(searchSong.Id) && !string.IsNullOrEmpty(ls.Id) && ls.Id == searchSong.Id) ||
                            (ls.Name == searchSong.Name));

                        if (localMatch != null)
                        {
                            bool installedThroughManager = localMatch.UpdatedAt > 0;
                            
                            if (!installedThroughManager)
                            {
                                searchSong.ButtonText = "Local";
                                searchSong.DownloadEnabled = false;
                            }
                            else
                            {
                                long serverUpdatedAtSeconds = searchSong.UpdatedAt / 1000;
                                
                                if (serverUpdatedAtSeconds > localMatch.UpdatedAt)
                                {
                                    searchSong.ButtonText = "Update";
                                    searchSong.DownloadEnabled = true;
                                }
                                else
                                {
                                    searchSong.ButtonText = "Remove";
                                    searchSong.DownloadEnabled = true;
                                }
                            }
                        }
                        else
                        {
                            searchSong.ButtonText = "Download";
                            searchSong.DownloadEnabled = true;
                        }
                    }

                    SearchResultsDataGrid.ItemsSource = paginatedResponse.Songs;
                    totalSearchPages = paginatedResponse.TotalPages;
                    totalSearchResults = paginatedResponse.Total;
                    
                    UpdateSearchPaginationUI();
                    StatusBar.Update($"Found {totalSearchResults} song(s)");
                }
                else
                {
                    SearchResultsDataGrid.ItemsSource = null;
                    totalSearchPages = 0;
                    totalSearchResults = 0;
                    UpdateSearchPaginationUI();
                    StatusBar.Update("No songs found");
                }
            }
            catch (Exception)
            {
                StatusBar.Update("Error searching songs");
            }
        }

        private void UpdateSearchPaginationUI()
        {
            SearchPageInfo.Text = $"Page {currentSearchPage} of {totalSearchPages} ({totalSearchResults} songs)";
            SearchPreviousButton.IsEnabled = currentSearchPage > 1;
            SearchNextButton.IsEnabled = currentSearchPage < totalSearchPages;
        }

        private async void SearchPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSearchPage > 1)
            {
                currentSearchPage--;
                await PerformSearchAsync();
            }
        }

        private async void SearchNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSearchPage < totalSearchPages)
            {
                currentSearchPage++;
                await PerformSearchAsync();
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all search text boxes
            SearchNameTextBox.Text = string.Empty;
            SearchArtistTextBox.Text = string.Empty;
            SearchGenreTextBox.Text = string.Empty;
            SearchBPMTextBox.Text = string.Empty;
            SearchAuthorTextBox.Text = string.Empty;

            // Clear search results
            SearchResultsDataGrid.ItemsSource = null;
            
            // Reset search pagination
            currentSearchPage = 1;
            totalSearchPages = 0;
            totalSearchResults = 0;
            lastSearchName = null;
            lastSearchArtist = null;
            lastSearchGenre = null;
            lastSearchBPM = null;
            lastSearchAuthor = null;
            UpdateSearchPaginationUI();

            StatusBar.Update("Search cleared");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SettingsManager.SaveAll();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reset scroll position for About tab when it's selected
            if (tabControl.SelectedIndex == 4) // About tab is at index 4 (0-indexed: Songs, Search, Manage, Settings, About)
            {
                // Reset scroll position to top
                AboutScrollViewer?.ScrollToTop();
            }
        }

        private void DiscordSupportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.gg/y76xhCSnBM",
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to open Discord link. Please visit: https://discord.gg/y76xhCSnBM");
            }
        }

        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null)
                return null;
                
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }

                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
