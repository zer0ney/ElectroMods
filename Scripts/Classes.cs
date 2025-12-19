using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ElectroMods.Scripts
{
    public class Classes
    {
        public class Song : INotifyPropertyChanged
        {
            private string _buttonText = "Download";
            
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            
            [JsonPropertyName("artists")]
            public string Artists { get; set; } = string.Empty;
            
            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;
            
            [JsonPropertyName("genres")]
            public string Genres { get; set; } = string.Empty;
            
            [JsonPropertyName("bpm")]
            public string BPM { get; set; } = string.Empty;
            
            [JsonPropertyName("author")]
            public string Author { get; set; } = string.Empty;
            
            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;
            
            [JsonPropertyName("downloadEnabled")]
            public bool DownloadEnabled { get; set; }
            
            [JsonPropertyName("downloadLink")]
            public string DownloadLink { get; set; } = string.Empty;
            
            [JsonPropertyName("updatedAt")]
            public long UpdatedAt { get; set; }
            
            [JsonPropertyName("downloads")]
            public int Downloads { get; set; }
            
            // UI-specific property for button text
            public string ButtonText
            {
                get => _buttonText;
                set
                {
                    if (_buttonText != value)
                    {
                        _buttonText = value;
                        OnPropertyChanged(nameof(ButtonText));
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class PaginatedResponse
        {
            [JsonPropertyName("page")]
            public int Page { get; set; }

            [JsonPropertyName("limit")]
            public int Limit { get; set; }

            [JsonPropertyName("total")]
            public int Total { get; set; }

            [JsonPropertyName("totalPages")]
            public int TotalPages { get; set; }

            [JsonPropertyName("songs")]
            public List<Song> Songs { get; set; } = new List<Song>();
        }
    }
}
