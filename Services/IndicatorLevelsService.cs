using System.Text.Json;

namespace PROXIMAMOP.Services;

public class IndicatorLevelsService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private const string Url = "http://195.3.223.136:5050/api/indicator-levels";

    public async Task<JsonElement?> GetIndicatorLevelsAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync(Url);

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}