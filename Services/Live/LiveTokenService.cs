using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PROXIMAMOP.Features.Live;
using PROXIMAMOP.Models.Live;

namespace PROXIMAMOP.Services.Live;

public sealed class LiveTokenService
{
    private readonly HttpClient _httpClient;

    public LiveTokenService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<LiveJoinResult> CreateJoinUrlAsync(
        string roomName,
        string userId,
        string displayName,
        string requestedRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return LiveJoinResult.Fail("roomName is required.");

        if (string.IsNullOrWhiteSpace(userId))
            return LiveJoinResult.Fail("userId is required.");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = userId;

        if (string.IsNullOrWhiteSpace(requestedRole))
            requestedRole = "viewer";

        roomName = roomName.Trim();
        userId = userId.Trim();
        displayName = displayName.Trim();
        requestedRole = requestedRole.Trim().ToLowerInvariant();

        try
        {
            var tokenUrl =
                $"{LiveConfig.TokenEndpoint}" +
                $"?room={Uri.EscapeDataString(roomName)}" +
                $"&identity={Uri.EscapeDataString(userId)}" +
                $"&displayName={Uri.EscapeDataString(displayName)}" +
                $"&requestedRole={Uri.EscapeDataString(requestedRole)}";

            using var response = await _httpClient.GetAsync(tokenUrl, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"Token request failed: {(int)response.StatusCode}";
                if (!string.IsNullOrWhiteSpace(raw))
                    errorMessage += $" - {raw}";

                return LiveJoinResult.Fail(errorMessage);
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<LiveTokenResponse>(
                cancellationToken: cancellationToken);

            if (tokenResponse is null)
                return LiveJoinResult.Fail("Server returned empty token response.");

            if (string.IsNullOrWhiteSpace(tokenResponse.Token))
                return LiveJoinResult.Fail("Token is missing in server response.");

            var liveKitUrl = string.IsNullOrWhiteSpace(tokenResponse.LiveKitUrl)
                ? LiveConfig.LiveKitUrl
                : tokenResponse.LiveKitUrl.Trim();

            var htmlUrl = string.IsNullOrWhiteSpace(tokenResponse.HtmlUrl)
                ? LiveConfig.HtmlUrl
                : tokenResponse.HtmlUrl.Trim();

            var finalRole = string.IsNullOrWhiteSpace(tokenResponse.Role)
                ? requestedRole
                : tokenResponse.Role.Trim().ToLowerInvariant();

            var joinUrl =
                $"{htmlUrl}" +
                $"?token={Uri.EscapeDataString(tokenResponse.Token)}" +
                $"&url={Uri.EscapeDataString(liveKitUrl)}" +
                $"&room={Uri.EscapeDataString(roomName)}" +
                $"&user={Uri.EscapeDataString(userId)}" +
                $"&name={Uri.EscapeDataString(displayName)}" +
                $"&role={Uri.EscapeDataString(finalRole)}";

            return LiveJoinResult.Success(
                joinUrl: joinUrl,
                token: tokenResponse.Token,
                roomName: roomName,
                userId: userId,
                displayName: displayName,
                role: finalRole,
                liveKitUrl: liveKitUrl,
                htmlUrl: htmlUrl,
                canMic: tokenResponse.CanMic,
                canCamera: tokenResponse.CanCamera,
                canScreen: tokenResponse.CanScreen,
                canPublish: tokenResponse.CanPublish);
        }
        catch (TaskCanceledException)
        {
            return LiveJoinResult.Fail("Request timed out.");
        }
        catch (Exception ex)
        {
            return LiveJoinResult.Fail($"Unexpected error: {ex.Message}");
        }
    }

    private sealed class LiveTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("canPublish")]
        public bool CanPublish { get; set; }

        [JsonPropertyName("canMic")]
        public bool CanMic { get; set; }

        [JsonPropertyName("canCamera")]
        public bool CanCamera { get; set; }

        [JsonPropertyName("canScreen")]
        public bool CanScreen { get; set; }

        [JsonPropertyName("liveKitUrl")]
        public string LiveKitUrl { get; set; } = string.Empty;

        [JsonPropertyName("htmlUrl")]
        public string HtmlUrl { get; set; } = string.Empty;
    }
}