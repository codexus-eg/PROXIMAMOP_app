using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class ChatService
{
    private const string BaseUrl = "http://193.34.213.124:5000";

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(BaseUrl)
    };

    private HubConnection? _hubConnection;

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

    public string GetSavedName()
    {
        return Preferences.Get("chat_user_name", "");
    }

    public void SaveName(string name)
    {
        Preferences.Set("chat_user_name", name);
    }

    public async Task<ChatUserDto?> RegisterOrUpdateAsync(string deviceId, string name)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/users/register-request",
                new RegisterUserRequest
                {
                    DeviceId = deviceId,
                    Name = name
                });

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ChatUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ChatUserDto?> GetMeAsync(string deviceId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/users/me?deviceId={Uri.EscapeDataString(deviceId)}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ChatUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ChatUserDto?> GetProfileByIdAsync(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/users/profile/{userId}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ChatUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ChatUserDto?> UpdateProfileAsync(string deviceId, string name, string bio)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                "/api/users/profile",
                new UpdateProfileRequest
                {
                    DeviceId = deviceId,
                    Name = name,
                    Bio = bio
                });

            if (!response.IsSuccessStatusCode)
                return null;

            SaveName(name);

            return await response.Content.ReadFromJsonAsync<ChatUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ChatUserDto?> UploadAvatarAsync(string deviceId, FileResult fileResult)
    {
        try
        {
            await using var stream = await fileResult.OpenReadAsync();

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(deviceId), "deviceId");

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", fileResult.FileName);

            var response = await _httpClient.PostAsync("/api/users/avatar", content);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ChatUserDto>();
        }
        catch
        {
            return null;
        }
    }
    public async Task<List<ChatMessageDto>> GetMessagesAsync(int take = 100, long? beforeMessageId = null)
    {
        try
        {
            var url = $"/api/chat/messages?take={take}";

            if (beforeMessageId.HasValue && beforeMessageId.Value > 0)
                url += $"&beforeMessageId={beforeMessageId.Value}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<ChatMessageDto>();

            return await response.Content.ReadFromJsonAsync<List<ChatMessageDto>>()
                   ?? new List<ChatMessageDto>();
        }
        catch
        {
            return new List<ChatMessageDto>();
        }
    }

    public async Task<bool> SendMessageAsync(string deviceId, string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/chat/send",
                new SendMessageRequest
                {
                    DeviceId = deviceId,
                    Text = text
                });

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendImageAsync(string deviceId, FileResult fileResult, string caption = "")
    {
        try
        {
            await using var stream = await fileResult.OpenReadAsync();

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(deviceId), "deviceId");
            content.Add(new StringContent(caption ?? ""), "text");

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", fileResult.FileName);

            var response = await _httpClient.PostAsync("/api/chat/send-image", content);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendVoiceAsync(string deviceId, string filePath, int? durationSeconds = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            await using var stream = File.OpenRead(filePath);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(deviceId), "deviceId");

            if (durationSeconds.HasValue)
                content.Add(new StringContent(durationSeconds.Value.ToString()), "durationSeconds");

            var fileName = Path.GetFileName(filePath);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetAudioMimeType(fileName));
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/chat/send-voice", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Stream?> DownloadFileStreamAsync(string url)
    {
        try
        {
            var fixedUrl = FixFileUrl(url);
            var memory = new MemoryStream();

            await using var stream = await _httpClient.GetStreamAsync(fixedUrl);
            await stream.CopyToAsync(memory);
            memory.Position = 0;

            return memory;
        }
        catch
        {
            return null;
        }
    }

    public async Task StartSignalRAsync(
        string deviceId,
        Action<ChatMessageDto> onReceive,
        Action<long> onDelete)
    {
        if (_hubConnection is not null &&
            (_hubConnection.State == HubConnectionState.Connected ||
             _hubConnection.State == HubConnectionState.Connecting ||
             _hubConnection.State == HubConnectionState.Reconnecting))
        {
            return;
        }
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{BaseUrl}/hubs/chat?deviceId={Uri.EscapeDataString(deviceId)}")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<ChatMessageDto>("ReceiveMessage", onReceive);
        _hubConnection.On<long>("MessageDeleted", onDelete);

        await _hubConnection.StartAsync();
    }

    public async Task StopSignalRAsync()
    {
        if (_hubConnection is null)
            return;

        try
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
        catch
        {
        }

        _hubConnection = null;
    }

    public string FixAvatarUrl(string avatarUrl)
    {
        return FixFileUrl(avatarUrl);
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

    public string GetBadgeDisplayName(string? badgeType)
    {
        return NormalizeBadge(badgeType) switch
        {
            "Bronze" => "Bronze",
            "Silver" => "Silver",
            "Gold" => "Gold",
            "Diamond" => "Diamond",
            "Titanium" => "Titanium",
            _ => "Normal"
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

    private static string GetAudioMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".aac" => "audio/aac",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };
    }
}