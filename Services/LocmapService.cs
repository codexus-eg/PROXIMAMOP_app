using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class LocmapService
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private const string BaseUrl = "http://195.3.223.136:7070";

    public async Task<List<LocmapLevel>> GetSnapshotAsync()
    {
        try
        {
            var url = $"{BaseUrl}/api/heatmap/snapshot?symbol=XAUUSD&minutes=30";
            var json = await _http.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<LocmapLevel>>(json, options) ?? new();
        }
        catch
        {
            return new();
        }
    }
}