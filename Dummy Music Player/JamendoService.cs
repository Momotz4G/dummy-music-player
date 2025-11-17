using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

public class JamendoResponse
{
    [JsonPropertyName("results")]
    public List<JamendoTrack> Results { get; set; }
}

public class JamendoTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("artist_name")]
    public string Artist { get; set; }

    [JsonPropertyName("album_name")]
    public string Album { get; set; }

    [JsonPropertyName("image")]
    public string ImageUrl { get; set; } // This is the album art!

    [JsonPropertyName("audio")]
    public string AudioUrl { get; set; } // This is the stream link!

    // Helper property to store the downloaded art
    public BitmapImage AlbumArt { get; set; }
}


// --- The Service Class ---
public class JamendoService
{
    private readonly HttpClient _client = new HttpClient();

    // =========================================================
    // !!! PASTE YOUR JAMENDO client_id HERE !!!
    // =========================================================
    private readonly string _clientId = "94b0e3c3";
    private readonly string _baseUrl = "https://api.jamendo.com/v3.0";

    /// <summary>
    /// Searches for tracks on Jamendo.
    /// </summary>
    public async Task<List<JamendoTrack>> SearchMusic(string searchTerm)
    {
        string searchUrl = $"{_baseUrl}/tracks/?client_id={_clientId}&format=jsonpretty&search={Uri.EscapeDataString(searchTerm)}";

        try
        {
            string jsonBody = await _client.GetStringAsync(searchUrl);
            var jamendoResponse = JsonSerializer.Deserialize<JamendoResponse>(jsonBody);

            if (jamendoResponse?.Results == null)
            {
                return new List<JamendoTrack>(); // Return empty list
            }

            // Download the album art for each track
            // ... (inside SearchMusic method) ...

            foreach (var track in jamendoResponse.Results)
            {
                if (track.ImageUrl != null)
                    track.ImageUrl = track.ImageUrl.Replace("http://", "https://");

                if (track.AudioUrl != null)
                    track.AudioUrl = track.AudioUrl.Replace("http://", "https://");

                track.AlbumArt = await DownloadImageAsync(track.ImageUrl);
            }

            return jamendoResponse.Results.Where(track => !string.IsNullOrEmpty(track.AudioUrl)).ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Jamendo API Error: {e.Message}");
            throw; // Let the UI handle the error
        }
    }

    /// <summary>
    /// Downloads an image from a URL and returns it as a BitmapImage.
    /// </summary>
    private async Task<BitmapImage> DownloadImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            byte[] imageBytes = await _client.GetByteArrayAsync(imageUrl);
            using (var stream = new MemoryStream(imageBytes))
            {
                stream.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Image Download Error: {e.Message}");
            return null;
        }
    }
}