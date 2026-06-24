using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class NewMarketServerService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private const string BaseUrl = "http://195.3.223.136:5050/api/market";

    public async Task<List<CandlePoint>> GetCandlesAsync(string symbol, string timeframe)
    {
        var url = $"{BaseUrl}/candles?symbol={symbol}&tf={timeframe}";
        var json = await _httpClient.GetStringAsync(url);

        return JsonSerializer.Deserialize<List<CandlePoint>>(json) ?? new List<CandlePoint>();
    }

    public async Task<decimal?> GetLatestPriceAsync(string symbol)
    {
        var url = $"{BaseUrl}/price?symbol={symbol}";
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