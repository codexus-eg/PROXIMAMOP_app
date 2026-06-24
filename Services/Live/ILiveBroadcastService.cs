using PROXIMAMOP.Models.Live;

namespace PROXIMAMOP.Services.Live;

public interface ILiveBroadcastService
{
    Task InitializeAsync();

    Task<LiveJoinResult> JoinAsync(
        LiveJoinRequest request,
        CancellationToken cancellationToken = default);

    Task LeaveAsync(CancellationToken cancellationToken = default);

    Task SetMicrophoneEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);

    Task SetCameraEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);

    Task RequestMicrophoneAsync(
        string roomName,
        string userId,
        CancellationToken cancellationToken = default);

    bool IsConnected { get; }
    bool IsMicrophoneEnabled { get; }
    bool IsCameraEnabled { get; }
}