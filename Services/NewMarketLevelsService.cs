using System.Text.Json;

namespace PROXIMAMOP.Services;

public class NewMarketLevelsService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private const string BaseUrl = "http://195.3.223.136:5050/api/market";

    public async Task<JsonElement?> GetLevelsAsync(string symbol)
    {
        try
        {
            var url = $"{BaseUrl}/levels?symbol={symbol}";
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