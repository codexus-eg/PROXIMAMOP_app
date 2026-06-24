using System.Text.Json;

namespace PROXIMAMOP.Services;

public class LevelsService
{
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "http://195.3.223.182:5002";

    public LevelsService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<JsonElement?> GetLevelsAsync()
    {
        try
        {
            var url = $"{BaseUrl}/levels";
            var json = await _httpClient.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}