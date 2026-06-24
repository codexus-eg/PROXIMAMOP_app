using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class FeedService
{
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "http://195.3.223.182:5003";

    public FeedService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        _httpClient.DefaultRequestHeaders.CacheControl =
            new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };
    }

    public async Task<List<FeedItem>> GetFeedAsync()
    {
        try
        {
            var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await _httpClient.GetAsync($"{BaseUrl}/feed?t={cacheBust}");

            if (!response.IsSuccessStatusCode)
                return new List<FeedItem>();

            var json = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<FeedItem>>(json, options) ?? new List<FeedItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            return new List<FeedItem>();
        }
    }

    public async Task<FeedItem?> GetLatestAsync()
    {
        try
        {
            var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await _httpClient.GetAsync($"{BaseUrl}/latest?t={cacheBust}");

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();

            if (json.Contains("\"empty\":true", StringComparison.OrdinalIgnoreCase))
                return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<FeedItem>(json, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            return null;
        }
    }
}