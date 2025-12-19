using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ElectroMods.Scripts
{
    class SongRetriever
    {
        public static List<Classes.Song> GetLocalSongData()
        {
            List<Classes.Song> localSongList = new List<Classes.Song>();

            string modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronauts/Mods");
            
            if (!Directory.Exists(modsPath))
            {
                return localSongList;
            }

            var modDirectory = new DirectoryInfo(modsPath);
            foreach (var songFolder in modDirectory.GetDirectories())
            {
                try
                {
                    var song = ParseSongConfig(songFolder);
                    if (song != null)
                    {
                        song.DownloadEnabled = false;
                        localSongList.Add(song);
                    }
                }
                catch (Exception)
                {
                    // Silently continue if a song folder fails to parse
                }
            }
            return localSongList;
        }

        public static Classes.Song ParseSongConfig(DirectoryInfo songFolder)
        {
            var song = new Classes.Song
            {
                Name = "Unknown",
                Artists = "Unknown",
                Description = "Unknown",
                Genres = "Unknown",
                BPM = "Unknown",
                Author = "Unknown",
                Version = "Unknown",
                UpdatedAt = 0,
                Id = null
            };

            string configPath = Path.Combine(songFolder.FullName, "Config.txt");
            if (!File.Exists(configPath))
            {
                return null;
            }

            var songConfig = File.ReadAllText(configPath).Split("\r\n");

            foreach (var line in songConfig)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] keyvalue = line.Split(":", 2);
                if (keyvalue.Length != 2)
                    continue;

                string key = keyvalue[0].Trim().TrimStart('m', '_');
                string value = keyvalue[1].Trim();

                switch (key)
                {
                    case "name":
                        song.Name = value;
                        break;
                    case "artists":
                        song.Artists = value;
                        break;
                    case "description":
                        song.Description = value;
                        break;
                    case "genres":
                        song.Genres = value;
                        break;
                    case "BPM":
                        song.BPM = value;
                        break;
                    case "author":
                        song.Author = value;
                        break;
                    case "version":
                        song.Version = value;
                        break;
                }
            }

            string metadataPath = Path.Combine(songFolder.FullName, ".electronauts_metadata.txt");
            if (File.Exists(metadataPath))
            {
                try
                {
                    var metadataLines = File.ReadAllLines(metadataPath);
                    foreach (var line in metadataLines)
                    {
                        if (line.StartsWith("song_id:"))
                        {
                            song.Id = line.Substring("song_id:".Length).Trim();
                        }
                        else if (line.StartsWith("downloaded_at:"))
                        {
                            if (long.TryParse(line.Substring("downloaded_at:".Length).Trim(), out long timestamp))
                            {
                                song.UpdatedAt = timestamp;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Silently continue if metadata file fails to parse
                }
            }

            return song;
        }

        public static List<Classes.Song> GetOnlineSongData()
        {
            try
            {
                return GetOnlineSongDataAsync().Result;
            }
            catch (Exception)
            {
                return new List<Classes.Song>();
            }
        }

        public static async Task<List<Classes.Song>> GetOnlineSongDataAsync()
        {
            var paginatedResponse = await ApiClient.GetAllSongsAsync();
            return paginatedResponse?.Songs ?? new List<Classes.Song>();
        }
        
        public static List<Classes.Song> GetAllSongs()
        {
            List<Classes.Song> localSongs = GetLocalSongData();
            List<Classes.Song> onlineSongs = GetOnlineSongData();

            if (onlineSongs == null || onlineSongs.Count == 0)
            {
                foreach (var song in localSongs)
                {
                    if (song.UpdatedAt == 0)
                    {
                        song.ButtonText = "Local";
                        song.DownloadEnabled = false;
                    }
                    else
                    {
                        song.ButtonText = "Remove";
                        song.DownloadEnabled = true;
                    }
                }
                return localSongs;
            }

            List<Classes.Song> localSongsToRemove = new List<Classes.Song>();
            List<Classes.Song> onlineSongsToRemove = new List<Classes.Song>();
            HashSet<Classes.Song> processedOnlineSongs = new HashSet<Classes.Song>();

            foreach (var localSong in localSongs)
            {
                bool foundOnline = false;
                
                foreach (var onlineSong in onlineSongs)
                {
                    bool isMatch = !string.IsNullOrEmpty(localSong.Id) && !string.IsNullOrEmpty(onlineSong.Id)
                        ? localSong.Id == onlineSong.Id
                        : localSong.Name == onlineSong.Name;
                    
                    if (isMatch)
                    {
                        foundOnline = true;
                        processedOnlineSongs.Add(onlineSong);
                        
                        bool installedThroughManager = localSong.UpdatedAt > 0;
                        
                        if (!installedThroughManager)
                        {
                            localSong.ButtonText = "Local";
                            localSong.DownloadEnabled = false;
                            onlineSongsToRemove.Add(onlineSong);
                        }
                        else
                        {
                            long serverUpdatedAtSeconds = onlineSong.UpdatedAt / 1000;
                            
                            if (serverUpdatedAtSeconds > localSong.UpdatedAt)
                            {
                                onlineSong.DownloadEnabled = true;
                                onlineSong.ButtonText = "Update";
                                localSongsToRemove.Add(localSong);
                            }
                            else
                            {
                                localSong.Downloads = onlineSong.Downloads;
                                localSong.ButtonText = "Remove";
                                localSong.DownloadEnabled = true;
                                onlineSongsToRemove.Add(onlineSong);
                            }
                        }
                        break;
                    }
                }
                
                if (!foundOnline)
                {
                    if (localSong.UpdatedAt == 0)
                    {
                        localSong.ButtonText = "Local";
                        localSong.DownloadEnabled = false;
                    }
                    else
                    {
                        localSong.ButtonText = "Remove";
                        localSong.DownloadEnabled = true;
                    }
                }
            }

            foreach (var onlineSong in onlineSongs)
            {
                if (!processedOnlineSongs.Contains(onlineSong))
                {
                    onlineSong.ButtonText = "Download";
                    onlineSong.DownloadEnabled = true;
                }
            }

            localSongs = localSongs.Except(localSongsToRemove).ToList();
            onlineSongs = onlineSongs.Except(onlineSongsToRemove).ToList();
            List<Classes.Song> onlineAndLocal = localSongs.Union(onlineSongs).ToList();

            return onlineAndLocal;
        }
        
        public static async Task<List<Classes.Song>> GetAllSongsAsync()
        {
            List<Classes.Song> localSongs = GetLocalSongData();
            List<Classes.Song> onlineSongs = await GetOnlineSongDataAsync();

            if (onlineSongs == null || onlineSongs.Count == 0)
            {
                foreach (var song in localSongs)
                {
                    if (song.UpdatedAt == 0)
                    {
                        song.ButtonText = "Local";
                        song.DownloadEnabled = false;
                    }
                    else
                    {
                        song.ButtonText = "Remove";
                        song.DownloadEnabled = true;
                    }
                }
                return localSongs;
            }

            List<Classes.Song> localSongsToRemove = new List<Classes.Song>();
            List<Classes.Song> onlineSongsToRemove = new List<Classes.Song>();
            HashSet<Classes.Song> processedOnlineSongs = new HashSet<Classes.Song>();

            foreach (var localSong in localSongs)
            {
                bool foundOnline = false;
                
                foreach (var onlineSong in onlineSongs)
                {
                    bool isMatch = !string.IsNullOrEmpty(localSong.Id) && !string.IsNullOrEmpty(onlineSong.Id)
                        ? localSong.Id == onlineSong.Id
                        : localSong.Name == onlineSong.Name;
                    
                    if (isMatch)
                    {
                        foundOnline = true;
                        processedOnlineSongs.Add(onlineSong);
                        
                        bool installedThroughManager = localSong.UpdatedAt > 0;
                        
                        if (!installedThroughManager)
                        {
                            localSong.ButtonText = "Local";
                            localSong.DownloadEnabled = false;
                            onlineSongsToRemove.Add(onlineSong);
                        }
                        else
                        {
                            long serverUpdatedAtSeconds = onlineSong.UpdatedAt / 1000;
                            
                            if (serverUpdatedAtSeconds > localSong.UpdatedAt)
                            {
                                onlineSong.DownloadEnabled = true;
                                onlineSong.ButtonText = "Update";
                                localSongsToRemove.Add(localSong);
                            }
                            else
                            {
                                localSong.Downloads = onlineSong.Downloads;
                                localSong.ButtonText = "Remove";
                                localSong.DownloadEnabled = true;
                                onlineSongsToRemove.Add(onlineSong);
                            }
                        }
                        break;
                    }
                }
                
                if (!foundOnline)
                {
                    if (localSong.UpdatedAt == 0)
                    {
                        localSong.ButtonText = "Local";
                        localSong.DownloadEnabled = false;
                    }
                    else
                    {
                        localSong.ButtonText = "Remove";
                        localSong.DownloadEnabled = true;
                    }
                }
            }

            foreach (var onlineSong in onlineSongs)
            {
                if (!processedOnlineSongs.Contains(onlineSong))
                {
                    onlineSong.ButtonText = "Download";
                    onlineSong.DownloadEnabled = true;
                }
            }

            localSongs = localSongs.Except(localSongsToRemove).ToList();
            onlineSongs = onlineSongs.Except(onlineSongsToRemove).ToList();
            List<Classes.Song> onlineAndLocal = localSongs.Union(onlineSongs).ToList();

            return onlineAndLocal;
        }
    }
}
