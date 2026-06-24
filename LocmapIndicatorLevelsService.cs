using System.Text.Json;
using System.Text.Json.Serialization;

namespace PROXIMAMOP.Services;

public sealed class LocmapIndicatorLevelsService
{
    private const string Url = "http://195.3.223.136:5050/api/indicator-levels";

    private readonly HttpClient _httpClient;

    public LocmapIndicatorLevelsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<LocmapIndicatorLevel>> GetHv5MainLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(Url, cancellationToken);

            var response = JsonSerializer.Deserialize<Response>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var objects = response?.Data?.Objects ?? [];

            return objects
                .Where(x => x.Obj.Equals("TREND", StringComparison.OrdinalIgnoreCase))
                .Where(x => x.Name.StartsWith("HV5_LINE_", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Name.Contains("_D", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Name.Contains("_S", StringComparison.OrdinalIgnoreCase))
                .Where(x => x.P1 > 0)
                .Select(x => new LocmapIndicatorLevel
                {
                    Name = x.Name,
                    Price = Math.Round(x.P1, 2),
                    Color = x.Color,
                    Width = x.Width,
                    Time1 = x.T1,
                    Time2 = x.T2
                })
                .GroupBy(x => x.Price)
                .Select(g => g.OrderByDescending(x => x.Width).First())
                .OrderByDescending(x => x.Price)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private sealed class Response
    {
        [JsonPropertyName("data")]
        public ResponseData? Data { get; set; }
    }

    private sealed class ResponseData
    {
        [JsonPropertyName("objects")]
        public List<ResponseObject> Objects { get; set; } = [];
    }

    private sealed class ResponseObject
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("obj")]
        public string Obj { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("t1")]
        public long T1 { get; set; }

        [JsonPropertyName("t2")]
        public long T2 { get; set; }

        [JsonPropertyName("p1")]
        public double P1 { get; set; }

        [JsonPropertyName("p2")]
        public double P2 { get; set; }
    }
}

public sealed class LocmapIndicatorLevel
{
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string Color { get; set; } = string.Empty;
    public int Width { get; set; }
    public long Time1 { get; set; }
    public long Time2 { get; set; }
}