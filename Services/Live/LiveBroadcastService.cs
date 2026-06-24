using System.Net.Http.Json;
using PROXIMAMOP.Models.Live;

namespace PROXIMAMOP.Services.Live;

public sealed class LiveBroadcastService : ILiveBroadcastService
{
    private readonly LiveTokenService _liveTokenService;
    private readonly HttpClient _httpClient;

    public bool IsConnected { get; private set; }
    public bool IsMicrophoneEnabled { get; private set; }
    public bool IsCameraEnabled { get; private set; }

    public LiveBroadcastService(LiveTokenService liveTokenService)
    {
        _liveTokenService = liveTokenService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<LiveJoinResult> JoinAsync(
        LiveJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null)
                return LiveJoinResult.Fail("Join request is null.");

            if (string.IsNullOrWhiteSpace(request.RoomName))
                return LiveJoinResult.Fail("roomName is required.");

            if (string.IsNullOrWhiteSpace(request.UserId))
                return LiveJoinResult.Fail("userId is required.");

            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.UserId.Trim()
                : request.DisplayName.Trim();

            var tokenResult = await _liveTokenService.CreateJoinUrlAsync(
                roomName: request.RoomName.Trim(),
                userId: request.UserId.Trim(),
                displayName: displayName,
                requestedRole: "viewer",
                cancellationToken: cancellationToken);

            if (!tokenResult.IsSuccess)
                return tokenResult;

            IsConnected = true;
            IsMicrophoneEnabled = tokenResult.CanMic;
            IsCameraEnabled = tokenResult.CanCamera;

            return tokenResult;
        }
        catch (Exception ex)
        {
            return LiveJoinResult.Fail(ex.Message);
        }
    }

    public Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        IsMicrophoneEnabled = false;
        IsCameraEnabled = false;
        return Task.CompletedTask;
    }

    public Task SetMicrophoneEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        IsMicrophoneEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task SetCameraEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        IsCameraEnabled = enabled;
        return Task.CompletedTask;
    }

    public async Task RequestMicrophoneAsync(
        string roomName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            throw new InvalidOperationException("roomName is required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("userId is required.");

        var body = new
        {
            roomName = roomName.Trim(),
            userId = userId.Trim(),
            displayName = userId.Trim(),
            requestType = "mic"
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "https://streamflowapp.com/live/requests",
            body,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}