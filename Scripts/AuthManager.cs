using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ElectroMods.Scripts
{
    public class AuthManager
    {
        private const string DiscordClientId = "945321886324228096";
        private const string DiscordRedirectUri = "http://localhost:3000/callback";
        private const string DiscordAuthUrl = "https://discord.com/api/oauth2/authorize";
        private const string DiscordTokenUrl = "https://discord.com/api/oauth2/token";
        private const string DiscordUserUrl = "https://discord.com/api/users/@me";

        private static HttpListener? httpListener;
        private static TaskCompletionSource<string?>? authCodeTcs;

        /// <summary>
        /// Generate the Discord OAuth authorization URL
        /// </summary>
        public static string GetDiscordAuthUrl()
        {
            var scopes = new[] { "identify" };
            var scopeString = string.Join("%20", scopes);
            
            return $"{DiscordAuthUrl}?client_id={DiscordClientId}&redirect_uri={Uri.EscapeDataString(DiscordRedirectUri)}&response_type=code&scope={scopeString}";
        }

        /// <summary>
        /// Check if the specified port is available
        /// </summary>
        public static bool IsPortAvailable(int port)
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                listener.Stop();
                listener.Close();
                return true;
            }
            catch (HttpListenerException)
            {
                return false;
            }
        }

        /// <summary>
        /// Start listening for OAuth callback on localhost:3000
        /// </summary>
        public static async Task<string?> StartCallbackListenerAsync()
        {
            if (!IsPortAvailable(3000))
            {
                return null;
            }

            authCodeTcs = new TaskCompletionSource<string?>();
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:3000/");

            try
            {
                httpListener.Start();
                
                _ = HandleIncomingRequestAsync();

                string? authCode = await authCodeTcs.Task;
                return authCode;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                httpListener?.Stop();
                httpListener?.Close();
            }
        }

        private static async Task HandleIncomingRequestAsync()
        {
            try
            {
                if (httpListener == null) return;
                
                HttpListenerContext context = await httpListener.GetContextAsync();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string? authCode = request.QueryString["code"];

                if (!string.IsNullOrEmpty(authCode))
                {
                    string responseString = "<html><body><h1>Authorization successful!</h1><p>You can close this window and return to ElectroMods.</p></body></html>";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    authCodeTcs?.TrySetResult(authCode);
                }
                else
                {
                    string errorString = "<html><body><h1>Authorization failed!</h1><p>No authorization code received.</p></body></html>";
                    byte[] buffer = Encoding.UTF8.GetBytes(errorString);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    authCodeTcs?.TrySetResult(null);
                }
            }
            catch (Exception)
            {
                authCodeTcs?.TrySetResult(null);
            }
        }

        /// <summary>
        /// Exchange authorization code for Discord access token
        /// </summary>
        public static async Task<(string? accessToken, DiscordUser? user)> ExchangeCodeForTokenAsync(string authCode, string? clientSecret)
        {
            try
            {
                if (string.IsNullOrEmpty(clientSecret))
                {
                    clientSecret = ConfigManager.GetDiscordClientSecret();
                }

                if (string.IsNullOrEmpty(clientSecret))
                {
                    return (null, null);
                }

                using (var client = new HttpClient())
                {
                    var requestData = new Dictionary<string, string>
                    {
                        { "client_id", DiscordClientId },
                        { "client_secret", clientSecret },
                        { "grant_type", "authorization_code" },
                        { "code", authCode },
                        { "redirect_uri", DiscordRedirectUri }
                    };

                    var content = new FormUrlEncodedContent(requestData);
                    var response = await client.PostAsync(DiscordTokenUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        return (null, null);
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    string? accessToken = tokenData.GetProperty("access_token").GetString();

                    if (accessToken == null)
                        return (null, null);

                    var user = await GetUserInfoAsync(accessToken);
                    return (accessToken, user);
                }
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Get current Discord user information
        /// </summary>
        private static async Task<DiscordUser?> GetUserInfoAsync(string accessToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    var response = await client.GetAsync(DiscordUserUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    
                    var userData = JsonSerializer.Deserialize<DiscordUser>(responseBody);
                    return userData;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Verify that an access token is still valid
        /// </summary>
        public static async Task<bool> VerifyTokenAsync(string accessToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    var response = await client.GetAsync(DiscordUserUrl);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Discord user information model
    /// </summary>
    public class DiscordUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        
        [JsonPropertyName("discriminator")]
        public string Discriminator { get; set; } = string.Empty;
        
        [JsonPropertyName("global_name")]
        public string? GlobalName { get; set; }
        
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        /// <summary>
        /// Display name - uses global_name if available (new Discord system),
        /// otherwise falls back to username#discriminator (old system)
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(GlobalName))
                {
                    return GlobalName;
                }
                
                if (Discriminator != "0")
                {
                    return $"{Username}#{Discriminator}";
                }
                
                return Username;
            }
        }
    }
}
