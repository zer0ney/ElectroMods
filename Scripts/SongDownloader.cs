using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ElectroMods.Scripts
{
    internal class SongDownloader
    {
        /// <summary>
        /// Download and install a song from the API
        /// </summary>
        public static async Task<bool> DownloadSongAsync(Classes.Song song)
        {
            try
            {
                if (string.IsNullOrEmpty(song.Id))
                {
                    return false;
                }

                string modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronauts/Mods");
                Directory.CreateDirectory(modsPath);

                byte[] zipData = await ApiClient.DownloadSongAsync(song.Id);
                if (zipData == null || zipData.Length == 0)
                {
                    return false;
                }

                string tempPath = Path.Combine(Path.GetTempPath(), $"electronauts_song_{Guid.NewGuid()}.zip");
                await File.WriteAllBytesAsync(tempPath, zipData);

                string songInstallPath = Path.Combine(modsPath, song.Name.Replace(" ", "_"));
                
                if (Directory.Exists(songInstallPath))
                {
                    Directory.Delete(songInstallPath, true);
                }

                ZipFile.ExtractToDirectory(tempPath, songInstallPath);
                File.Delete(tempPath);

                SaveSongMetadata(songInstallPath, song.Id, song.UpdatedAt);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Save song metadata including song ID and downloaded_at timestamp
        /// </summary>
        private static void SaveSongMetadata(string songFolder, string songId, long updatedAt)
        {
            try
            {
                string metadataPath = Path.Combine(songFolder, ".electronauts_metadata.txt");
                // Save the current time as when we downloaded/installed this song
                long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string metadata = $"song_id:{songId}\ndownloaded_at:{currentTimestamp}\n";
                File.WriteAllText(metadataPath, metadata);
            }
            catch (Exception)
            {
                // Silently continue if metadata save fails
            }
        }

        /// <summary>
        /// Remove a downloaded song from the local mods folder
        /// </summary>
        public static bool RemoveSong(Classes.Song song)
        {
            try
            {
                string modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronauts/Mods");
                string songInstallPath = Path.Combine(modsPath, song.Name.Replace(" ", "_"));
                
                if (!Directory.Exists(songInstallPath))
                {
                    return false;
                }

                Directory.Delete(songInstallPath, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Upload a local song to the API
        /// </summary>
        public static async Task<bool> UploadSongAsync(string songFolderPath)
        {
            try
            {
                if (!Directory.Exists(songFolderPath))
                {
                    return false;
                }

                var songFolder = new DirectoryInfo(songFolderPath);
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"{songFolder.Name}_{Guid.NewGuid()}.zip");
                
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }

                ZipFile.CreateFromDirectory(songFolderPath, tempZipPath);

                bool uploadSuccess = await ApiClient.UploadSongAsync(tempZipPath, songFolder.Name, "");
                
                File.Delete(tempZipPath);

                return uploadSuccess;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
