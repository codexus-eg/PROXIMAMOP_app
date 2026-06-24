using System.Net.Http.Headers;
using System.Net.Http.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class MessengerService
{
    private const string BaseUrl = "http://193.34.213.126:5005";

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

    public async Task<MessengerUserDto?> SyncUserAsync(
        int mainAppUserId,
        string deviceId,
        string userName,
        string avatarUrl = "")
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/messenger/users/sync",
                new SyncMessengerUserRequest
                {
                    MainAppUserId = mainAppUserId,
                    DeviceId = deviceId,
                    UserName = userName,
                    AvatarUrl = avatarUrl ?? ""
                });

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<MessengerUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<MessengerUserDto?> GetMeAsync(string deviceId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/messenger/users/me?deviceId={Uri.EscapeDataString(deviceId)}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<MessengerUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ConversationListItemDto>> GetConversationsAsync(string deviceId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/messenger/conversations?deviceId={Uri.EscapeDataString(deviceId)}");

            if (!response.IsSuccessStatusCode)
                return new List<ConversationListItemDto>();

            return await response.Content.ReadFromJsonAsync<List<ConversationListItemDto>>()
                   ?? new List<ConversationListItemDto>();
        }
        catch
        {
            return new List<ConversationListItemDto>();
        }
    }

    public async Task<List<MessengerUserDto>> SearchUsersAsync(string deviceId, string query, int take = 20)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/messenger/users/search?deviceId={Uri.EscapeDataString(deviceId)}&query={Uri.EscapeDataString(query)}&take={take}");

            if (!response.IsSuccessStatusCode)
                return new List<MessengerUserDto>();

            return await response.Content.ReadFromJsonAsync<List<MessengerUserDto>>()
                   ?? new List<MessengerUserDto>();
        }
        catch
        {
            return new List<MessengerUserDto>();
        }
    }

    public async Task<ConversationListItemDto?> StartPrivateConversationAsync(string deviceId, int targetUserId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/messenger/conversations/start-private",
                new StartPrivateConversationRequest
                {
                    DeviceId = deviceId,
                    TargetUserId = targetUserId
                });

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ConversationListItemDto>();
        }
        catch
        {
            return null;
        }
    }
    public async Task<List<PrivateMessageDto>> GetMessagesAsync(
    string deviceId,
    long conversationId,
    int take = 40,
    long? beforeMessageId = null)
    {
        try
        {
            var url =
                $"/api/messenger/conversations/{conversationId}/messages" +
                $"?deviceId={Uri.EscapeDataString(deviceId)}" +
                $"&take={take}";

            if (beforeMessageId.HasValue && beforeMessageId.Value > 0)
                url += $"&beforeMessageId={beforeMessageId.Value}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<PrivateMessageDto>();

            return await response.Content.ReadFromJsonAsync<List<PrivateMessageDto>>()
                   ?? new List<PrivateMessageDto>();
        }
        catch
        {
            return new List<PrivateMessageDto>();
        }
    }

    public async Task<PrivateMessageDto?> SendTextAsync(string deviceId, long conversationId, string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/messenger/messages/text",
                new SendPrivateTextRequest
                {
                    DeviceId = deviceId,
                    ConversationId = conversationId,
                    Text = text
                });

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<PrivateMessageDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<PrivateMessageDto?> SendImageAsync(
        string deviceId,
        long conversationId,
        FileResult fileResult,
        string caption = "")
    {
        try
        {
            await using var stream = await fileResult.OpenReadAsync();

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(deviceId), "deviceId");
            content.Add(new StringContent(conversationId.ToString()), "conversationId");
            content.Add(new StringContent(caption ?? ""), "text");

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetImageMimeType(fileResult.FileName));
            content.Add(fileContent, "file", fileResult.FileName);

            var response = await _httpClient.PostAsync("/api/messenger/messages/image", content);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<PrivateMessageDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<PrivateMessageDto?> SendVoiceAsync(
        string deviceId,
        long conversationId,
        string filePath,
        int? durationSeconds = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            await using var stream = File.OpenRead(filePath);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(deviceId), "deviceId");
            content.Add(new StringContent(conversationId.ToString()), "conversationId");

            if (durationSeconds.HasValue)
                content.Add(new StringContent(durationSeconds.Value.ToString()), "durationSeconds");

            var fileName = Path.GetFileName(filePath);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetAudioMimeType(fileName));
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/messenger/messages/voice", content);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<PrivateMessageDto>();
        }
        catch
        {
            return null;
        }
    }
    public async Task<bool> MarkConversationReadAsync(string deviceId, long conversationId, long? lastReadMessageId = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/messenger/messages/read",
                new MarkConversationReadRequest
                {
                    DeviceId = deviceId,
                    ConversationId = conversationId,
                    LastReadMessageId = lastReadMessageId
                });

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

    public string FixFileUrl(string? url)
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

    public string FixAvatarUrl(string? avatarUrl)
    {
        return FixFileUrl(avatarUrl);
    }

    private static string GetImageMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
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