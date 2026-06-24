using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class ServerMarketService
{
    private readonly HttpClient _httpClient = new();

    private const string BaseUrl = "http://195.3.223.182:5001";

    public async Task<List<CandlePoint>> GetCandlesAsync(string timeframe)
    {
        var url = $"{BaseUrl}/candles?tf={timeframe}";
        var json = await _httpClient.GetStringAsync(url);

        var candles = JsonSerializer.Deserialize<List<CandlePoint>>(json);
        return candles ?? new List<CandlePoint>();
    }

    public async Task<decimal?> GetLatestPriceAsync()
    {
        var url = $"{BaseUrl}/price";
        var json = await _httpClient.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("bid", out var bidElement))
            return null;

        return bidElement.GetDecimal();
    }

    public static int TimeframeToSeconds(string timeframe) => timeframe switch
    {
        "M1" => 60,
        "M5" => 300,
        "M15" => 900,
        "M30" => 1800,
        "H1" => 3600,
        "H4" => 14400,
        "D1" => 86400,
        _ => 60
    };
}