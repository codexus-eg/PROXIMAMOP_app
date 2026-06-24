using System.Net.Http.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class AnalystService
{
    private const string BaseUrl = "http://193.34.213.124:5000";

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(BaseUrl)
    };

    public string BaseAddress => BaseUrl;

    public string GetOrCreateDeviceId()
    {
        var id = Preferences.Get("chat_device_id", "");

        if (!string.IsNullOrWhiteSpace(id))
            return id;

        id = Guid.NewGuid().ToString("N");
        Preferences.Set("chat_device_id", id);

        return id;
    }

    public string FixFileUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return $"{BaseUrl}{url}";
    }

    public string FixAvatarUrl(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return "dotnet_bot.png";

        return FixFileUrl(avatarUrl);
    }

    public async Task<List<AnalystDto>> GetAnalystsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/analysts");

            if (!response.IsSuccessStatusCode)
                return new List<AnalystDto>();

            return await response.Content.ReadFromJsonAsync<List<AnalystDto>>() ?? new List<AnalystDto>();
        }
        catch
        {
            return new List<AnalystDto>();
        }
    }

    public async Task<AnalystDto?> GetAnalystByIdAsync(int analystId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/analysts/{analystId}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AnalystDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AnalystPostsPageDto?> GetAnalystPostsAsync(int analystId, int page = 1, int pageSize = 20)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/analysts/{analystId}/posts?page={page}&pageSize={pageSize}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AnalystPostsPageDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AnalystPostDto?> GetAnalystPostByIdAsync(long postId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/analyst-posts/{postId}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AnalystPostDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AnalystCommentsPageDto?> GetPostCommentsAsync(long postId, int page = 1, int pageSize = 50)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/analyst-posts/{postId}/comments?page={page}&pageSize={pageSize}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AnalystCommentsPageDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AnalystCommentDto?> AddCommentAsync(long postId, string deviceId, string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/analyst-posts/{postId}/comments",
                new AddAnalystCommentRequest
                {
                    DeviceId = deviceId,
                    Text = text
                });

            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<AnalystCommentDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AnalystPostDto?> CreatePostAsync(string deviceId, string text, FileResult? fileResult = null)
    {
        try
        {
            await using Stream? stream = fileResult is null
                ? null
                : await fileResult.OpenReadAsync();

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(deviceId), "deviceId");
            content.Add(new StringContent(text ?? ""), "text");

            if (fileResult is not null && stream is not null)
            {
                var fileName = fileResult.FileName ?? "image.jpg";
                var ext = Path.GetExtension(fileName).ToLowerInvariant();

                var mimeType = ext switch
                {
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };

                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

                content.Add(fileContent, "file", fileName);
            }

            var response = await _httpClient.PostAsync("/api/analyst/posts/create", content);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AnalystPostDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteOwnPostAsync(long postId, string deviceId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/analyst/posts/{postId}/delete",
                new DeviceActionRequest
                {
                    DeviceId = deviceId
                });

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteOwnCommentAsync(long commentId, string deviceId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/analyst-comments/{commentId}/delete",
                new DeviceActionRequest
                {
                    DeviceId = deviceId
                });

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public string GetStarsText(int stars)
    {
        if (stars <= 0)
            return "☆☆☆☆☆";

        if (stars > 5)
            stars = 5;

        return new string('★', stars) + new string('☆', 5 - stars);
    }

    public string FormatDateTime(DateTime dateTimeUtc)
    {
        var local = dateTimeUtc.ToLocalTime();
        var now = DateTime.Now;
        var diff = now - local;

        if (diff.TotalMinutes < 1)
            return "الآن";

        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} د";

        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} س";

        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} يوم";

        return local.ToString("yyyy/MM/dd");
    }

    public bool IsFollowingAnalyst(int analystId)
    {
        return Preferences.Get(GetFollowKey(analystId), false);
    }

    public void SetFollowingAnalyst(int analystId, bool value)
    {
        Preferences.Set(GetFollowKey(analystId), value);

        if (!value)
            Preferences.Set(GetNotificationsKey(analystId), false);
    }

    public bool IsAnalystNotificationsEnabled(int analystId)
    {
        return Preferences.Get(GetNotificationsKey(analystId), false);
    }
    public void SetAnalystNotificationsEnabled(int analystId, bool value)
    {
        Preferences.Set(GetNotificationsKey(analystId), value);

        if (value)
            Preferences.Set(GetFollowKey(analystId), true);
    }

    public Color GetBadgeBorderColor(string? badgeType)
    {
        return NormalizeBadge(badgeType) switch
        {
            "Bronze" => Color.FromArgb("#B87333"),
            "Silver" => Color.FromArgb("#C0C0C0"),
            "Gold" => Color.FromArgb("#FFD700"),
            "Diamond" => Color.FromArgb("#52D8FF"),
            "Titanium" => Color.FromArgb("#A78BFA"),
            _ => Color.FromArgb("#2E2E2E")
        };
    }

    public Color GetBadgeBackgroundColor(string? badgeType)
    {
        return NormalizeBadge(badgeType) switch
        {
            "Bronze" => Color.FromArgb("#8A5A2B"),
            "Silver" => Color.FromArgb("#8B8B8B"),
            "Gold" => Color.FromArgb("#C7A500"),
            "Diamond" => Color.FromArgb("#1687B2"),
            "Titanium" => Color.FromArgb("#6D47D9"),
            _ => Color.FromArgb("#2E2E2E")
        };
    }

    public Color GetBadgeIconTextColor(string? badgeType)
    {
        return NormalizeBadge(badgeType) switch
        {
            "Silver" => Colors.Black,
            "Gold" => Colors.Black,
            _ => Colors.White
        };
    }

    public string GetBadgeIcon(string? badgeType)
    {
        return NormalizeBadge(badgeType) switch
        {
            "Bronze" => "🥉",
            "Silver" => "🥈",
            "Gold" => "👑",
            "Diamond" => "💎",
            "Titanium" => "✦",
            _ => ""
        };
    }

    public bool HasBadge(string? badgeType)
    {
        return !string.Equals(NormalizeBadge(badgeType), "None", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeBadge(string? badgeType)
    {
        return string.IsNullOrWhiteSpace(badgeType) ? "None" : badgeType.Trim();
    }

    private string GetFollowKey(int analystId) => $"analyst_follow_{analystId}";
    private string GetNotificationsKey(int analystId) => $"analyst_notify_{analystId}";
}