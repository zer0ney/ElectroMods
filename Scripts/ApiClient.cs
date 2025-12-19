using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Net;

namespace ElectroMods.Scripts
{
    public class ApiClient
    {
        private static readonly HttpClient httpClient = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(10)
        });
        private static string apiBaseUrl = "https://electromods-api.zer0ney.me/api";
        private static string? authToken;
        private static string? discordAccessToken;

        static ApiClient()
        {
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ElectroMods/1.0");
            
            apiBaseUrl = ConfigManager.GetApiBaseUrl();
        }

        public static void SetApiBaseUrl(string baseUrl)
        {
            apiBaseUrl = baseUrl;
        }

        public static void SetAuthToken(string? token)
        {
            authToken = token;
            if (string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Remove("Authorization");
            }
            else
            {
                httpClient.DefaultRequestHeaders.Remove("Authorization");
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            }
        }

        public static void SetDiscordAccessToken(string? token)
        {
            discordAccessToken = token;
        }

        public static string? GetDiscordAccessToken()
        {
            return discordAccessToken;
        }

        /// <summary>
        /// Get all available songs from the API with pagination
        /// </summary>
        public static async Task<Classes.PaginatedResponse> GetAllSongsAsync(int page = 1, int limit = 50)
        {
            try
            {
                string url = $"{apiBaseUrl}/songs?page={page}&limit={limit}";
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                Classes.PaginatedResponse paginatedResponse = JsonSerializer.Deserialize<Classes.PaginatedResponse>(content, options);

                return paginatedResponse ?? new Classes.PaginatedResponse { Songs = new List<Classes.Song>(), Page = page, Limit = limit, Total = 0, TotalPages = 0 };
            }
            catch (Exception)
            {
                return new Classes.PaginatedResponse { Songs = new List<Classes.Song>(), Page = page, Limit = limit, Total = 0, TotalPages = 0 };
            }
        }

        /// <summary>
        /// Search for songs with optional filters and pagination
        /// </summary>
        public static async Task<Classes.PaginatedResponse> SearchSongsAsync(int page = 1, int limit = 50, string? name = null, string? artist = null, string? genre = null, string? bpm = null, string? author = null)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"page={page}",
                    $"limit={limit}"
                };
                
                if (!string.IsNullOrWhiteSpace(name))
                    queryParams.Add($"name={Uri.EscapeDataString(name)}");
                if (!string.IsNullOrWhiteSpace(artist))
                    queryParams.Add($"artist={Uri.EscapeDataString(artist)}");
                if (!string.IsNullOrWhiteSpace(genre))
                    queryParams.Add($"genre={Uri.EscapeDataString(genre)}");
                if (!string.IsNullOrWhiteSpace(bpm))
                    queryParams.Add($"bpm={Uri.EscapeDataString(bpm)}");
                if (!string.IsNullOrWhiteSpace(author))
                    queryParams.Add($"author={Uri.EscapeDataString(author)}");

                string queryString = "?" + string.Join("&", queryParams);
                string url = $"{apiBaseUrl}/songs/search{queryString}";
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                Classes.PaginatedResponse? paginatedResponse = JsonSerializer.Deserialize<Classes.PaginatedResponse>(content, options);
                
                return paginatedResponse ?? new Classes.PaginatedResponse { Songs = new List<Classes.Song>(), Page = page, Limit = limit, Total = 0, TotalPages = 0 };
            }
            catch (Exception)
            {
                return new Classes.PaginatedResponse { Songs = new List<Classes.Song>(), Page = page, Limit = limit, Total = 0, TotalPages = 0 };
            }
        }

        /// <summary>
        /// Get a specific song by ID
        /// </summary>
        public static async Task<Classes.Song?> GetSongByIdAsync(string songId)
        {
            try
            {
                string url = $"{apiBaseUrl}/songs/{songId}";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                Classes.Song? song = JsonSerializer.Deserialize<Classes.Song>(content);

                return song;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Download a song ZIP file by ID
        /// </summary>
        public static async Task<byte[]?> DownloadSongAsync(string songId)
        {
            try
            {
                string url = $"{apiBaseUrl}/songs/{songId}/download";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                return fileBytes;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Upload a new song to the API
        /// </summary>
        public static async Task<bool> UploadSongAsync(string songFilePath, string songName, string songVersion)
        {
            try
            {
                if (!System.IO.File.Exists(songFilePath))
                {
                    return false;
                }

                string url = $"{apiBaseUrl}/songs/upload";
                
                using (var content = new MultipartFormDataContent())
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(songFilePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                    content.Add(fileContent, "file", System.IO.Path.GetFileName(songFilePath));
                    content.Add(new StringContent(songName), "name");
                    content.Add(new StringContent(songVersion), "version");

                    var response = await httpClient.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();

                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Upload a new song to the API with progress tracking
        /// </summary>
        public static async Task<(bool success, string message)> UploadSongWithProgressAsync(string songFilePath, IProgress<long> progress)
        {
            try
            {
                if (!System.IO.File.Exists(songFilePath))
                {
                    return (false, "Song file not found");
                }

                if (string.IsNullOrEmpty(discordAccessToken))
                {
                    return (false, "Not authenticated with Discord");
                }

                string url = $"{apiBaseUrl}/songs/upload";
                
                var fileInfo = new System.IO.FileInfo(songFilePath);

                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        byte[] fileBytes = System.IO.File.ReadAllBytes(songFilePath);
                        
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                        fileContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                        {
                            Name = "\"file\"",
                            FileName = "\"" + System.IO.Path.GetFileName(songFilePath) + "\""
                        };

                        content.Add(fileContent, "file", System.IO.Path.GetFileName(songFilePath));

                        requestMessage.Content = content;
                        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordAccessToken);

                        var handler = new ProgressMessageHandler(new SocketsHttpHandler
                        {
                            ConnectTimeout = TimeSpan.FromSeconds(5),
                            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
                            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(10)
                        }, progress);

                        using (var progressClient = new HttpClient(handler))
                        {
                            progressClient.Timeout = TimeSpan.FromSeconds(300);
                            
                            var response = await progressClient.SendAsync(requestMessage);

                            string responseBody = await response.Content.ReadAsStringAsync();

                            if (!response.IsSuccessStatusCode)
                            {
                                return (false, ParseErrorMessage(responseBody));
                            }

                            return (true, ParseSuccessMessage(responseBody));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Upload error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update an existing song with progress tracking
        /// </summary>
        public static async Task<(bool success, string message)> UpdateSongWithProgressAsync(string songId, string songFilePath, IProgress<long> progress)
        {
            try
            {
                if (!System.IO.File.Exists(songFilePath))
                {
                    return (false, "Song file not found");
                }

                if (string.IsNullOrEmpty(discordAccessToken))
                {
                    return (false, "Not authenticated with Discord");
                }

                string url = $"{apiBaseUrl}/songs/{songId}";
                
                var fileInfo = new System.IO.FileInfo(songFilePath);

                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        byte[] fileBytes = System.IO.File.ReadAllBytes(songFilePath);
                        
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                        fileContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                        {
                            Name = "\"file\"",
                            FileName = "\"" + System.IO.Path.GetFileName(songFilePath) + "\""
                        };

                        content.Add(fileContent, "file", System.IO.Path.GetFileName(songFilePath));

                        requestMessage.Content = content;
                        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordAccessToken);

                        var handler = new ProgressMessageHandler(new SocketsHttpHandler
                        {
                            ConnectTimeout = TimeSpan.FromSeconds(5),
                            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
                            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(10)
                        }, progress);

                        using (var progressClient = new HttpClient(handler))
                        {
                            progressClient.Timeout = TimeSpan.FromSeconds(300);
                            
                            var response = await progressClient.SendAsync(requestMessage);

                            string responseBody = await response.Content.ReadAsStringAsync();

                            if (!response.IsSuccessStatusCode)
                            {
                                return (false, ParseErrorMessage(responseBody));
                            }

                            return (true, ParseSuccessMessage(responseBody));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete a song uploaded by the current user
        /// </summary>
        public static async Task<(bool success, string message)> DeleteSongAsync(string songId)
        {
            try
            {
                if (string.IsNullOrEmpty(discordAccessToken))
                {
                    return (false, "Not authenticated with Discord");
                }

                string url = $"{apiBaseUrl}/songs/{songId}";
                
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Delete, url))
                {
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordAccessToken);
                    var response = await httpClient.SendAsync(requestMessage);

                    string content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return (false, ParseErrorMessage(content));
                    }

                    return (true, ParseSuccessMessage(content));
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting song: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all songs uploaded by the current user
        /// </summary>
        public static async Task<(bool success, List<Classes.Song>? songs, string message)> GetUserSongsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(discordAccessToken))
                {
                    return (false, null, "Not authenticated with Discord");
                }

                string url = $"{apiBaseUrl}/songs/user";
                
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordAccessToken);
                    var response = await httpClient.SendAsync(requestMessage);

                    string content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return (false, null, ParseErrorMessage(content));
                    }

                    List<Classes.Song>? songs = JsonSerializer.Deserialize<List<Classes.Song>>(content);
                    return (true, songs ?? new List<Classes.Song>(), "");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Error fetching songs: {ex.Message}");
            }
        }

        private static string ParseSuccessMessage(string responseBody)
        {
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseBody);
                
                if (jsonElement.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString() ?? "Unknown success response";
                }
            }
            catch (Exception)
            {
                // Silently continue
            }
            return "Operation successful";
        }

        private static string ParseErrorMessage(string responseBody)
        {
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseBody);
                
                if (jsonElement.TryGetProperty("error", out var errorProp))
                {
                    string errorMsg = errorProp.GetString() ?? "Unknown error";
                    
                    if (jsonElement.TryGetProperty("detail", out var detailProp))
                    {
                        string? detail = detailProp.GetString();
                        if (!string.IsNullOrEmpty(detail))
                        {
                            return $"{errorMsg}\n\nDetails: {detail}";
                        }
                    }
                    
                    return errorMsg;
                }
                
                if (jsonElement.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString() ?? "Unknown error";
                }
            }
            catch (Exception)
            {
                // Silently continue
            }
            return "Operation failed";
        }
    }

    /// <summary>
    /// Custom handler for tracking upload progress
    /// </summary>
    public class ProgressMessageHandler : DelegatingHandler
    {
        private IProgress<long> progress;

        public ProgressMessageHandler(HttpMessageHandler innerHandler, IProgress<long> progress) : base(innerHandler)
        {
            this.progress = progress;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is HttpContent content && progress != null)
            {
                var reportingContent = new ProgressContent(content, progress);
                request.Content = reportingContent;
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Wrapper around HttpContent to report progress
    /// </summary>
    public class ProgressContent : HttpContent
    {
        private HttpContent innerContent;
        private IProgress<long> progress;
        private const int BufferSize = 4096;

        public ProgressContent(HttpContent innerContent, IProgress<long> progress)
        {
            this.innerContent = innerContent;
            this.progress = progress;

            foreach (var header in innerContent.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[BufferSize];
            var totalBytes = innerContent.Headers.ContentLength ?? 0;
            var canReportProgress = totalBytes != 0;

            using (var contentStream = await innerContent.ReadAsStreamAsync())
            {
                var totalRead = 0L;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    totalRead += read;
                    await stream.WriteAsync(buffer, 0, read);

                    if (canReportProgress)
                    {
                        progress?.Report(totalRead);
                    }
                }
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = innerContent.Headers.ContentLength ?? 0;
            return length != 0;
        }
    }
}
