using System.Text.Json;

namespace PROXIMAMOP.Services;

public class CandleVelocityService
{
    private readonly HttpClient _httpClient = new();

    private const string BaseUrl =
        "http://195.3.223.134:6062/api/candlevelocity";

    public async Task<CandleVelocityResponse?> GetAsync(
        string symbol,
        string timeframe)
    {
        try
        {
            var url =
                $"{BaseUrl}?symbol={symbol}&timeframe={timeframe}";

            var json = await _httpClient.GetStringAsync(url);

            return JsonSerializer.Deserialize<CandleVelocityResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            return null;
        }
    }
}

public class CandleVelocityResponse
{
    public string symbol { get; set; } = "";
    public string timeframe { get; set; } = "";
    public double price { get; set; }

    public CandleVelocityCurrent current { get; set; } = new();

    public CandleVelocityPanels panels { get; set; } = new();
}

public class CandleVelocityCurrent
{
    public double maxSpeed { get; set; }
    public double prevMax { get; set; }
    public double avgSpeed { get; set; }

    public int ticks { get; set; }
    public double ticksPerSecond { get; set; }

    public int buyLop { get; set; }
    public int sellLop { get; set; }
    public int lopDiff { get; set; }

    public double buyVolSpeed { get; set; }
    public double sellVolSpeed { get; set; }
    public double volDiffSpeed { get; set; }
    public double totalVolSpeed { get; set; }
}

public class CandleVelocityPanels
{
    public List<CandleVelocitySpeedItem> speed { get; set; } = new();
    public List<CandleVelocityTickItem> ticks { get; set; } = new();
    public List<CandleVelocityVolumeItem> volume { get; set; } = new();

    public long lopBuySum { get; set; }
    public long lopSellSum { get; set; }
    public long lopDiff { get; set; }
    public string lopSide { get; set; } = "EQUAL";
}

public class CandleVelocitySpeedItem
{
    public int index { get; set; }
    public long time { get; set; }

    public double max { get; set; }
    public double avg { get; set; }
}

public class CandleVelocityTickItem
{
    public int index { get; set; }
    public long time { get; set; }

    public int ticks { get; set; }
    public double ticksPerSecond { get; set; }
}

public class CandleVelocityVolumeItem
{
    public int index { get; set; }
    public long time { get; set; }

    public double buyVolSpeed { get; set; }
    public double sellVolSpeed { get; set; }
    public double diffVolSpeed { get; set; }
}