using System.Globalization;
using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class GoldMarketService
{
    private readonly HttpClient _httpClient = new();
    private const string ApiKey = "4b568911e47d4a058ccde0414e0b0aa3";

    public async Task<List<CandlePoint>> GetCandlesAsync(string interval, int outputSize = 120)
    {
        var url =
            $"https://api.twelvedata.com/time_series?symbol=XAU/USD&interval={interval}&outputsize={outputSize}&apikey={ApiKey}";

        var json = await _httpClient.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("values", out var valuesElement))
            return new List<CandlePoint>();

        var list = new List<CandlePoint>();

        foreach (var item in valuesElement.EnumerateArray())
        {
            var dt = item.GetProperty("datetime").GetString() ?? "";
            var time = DateTime.Parse(dt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var unix = new DateTimeOffset(time).ToUnixTimeSeconds();

            list.Add(new CandlePoint
            {
                time = unix,
                open = decimal.Parse(item.GetProperty("open").GetString()!, CultureInfo.InvariantCulture),
                high = decimal.Parse(item.GetProperty("high").GetString()!, CultureInfo.InvariantCulture),
                low = decimal.Parse(item.GetProperty("low").GetString()!, CultureInfo.InvariantCulture),
                close = decimal.Parse(item.GetProperty("close").GetString()!, CultureInfo.InvariantCulture)
            });
        }

        list.Reverse();
        return list;
    }

    public async Task<decimal?> GetLatestPriceAsync()
    {
        var url = $"https://api.twelvedata.com/price?symbol=XAU/USD&apikey={ApiKey}";
        var json = await _httpClient.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("price", out var priceElement))
            return null;

        return decimal.Parse(priceElement.GetString()!, CultureInfo.InvariantCulture);
    }

    public static int IntervalToSeconds(string interval) => interval switch
    {
        "1min" => 60,
        "5min" => 300,
        "15min" => 900,
        "1h" => 3600,
        "4h" => 14400,
        "1day" => 86400,
        _ => 60
    };
}