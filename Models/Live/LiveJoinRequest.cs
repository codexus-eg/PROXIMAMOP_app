namespace PROXIMAMOP.Models.Live;

public sealed class LiveJoinRequest
{
    public required string RoomName { get; init; }
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string TokenEndpoint { get; init; }
    public required string LiveKitUrl { get; init; }

    public bool PublishMicrophoneOnJoin { get; init; } = false;
    public bool PublishCameraOnJoin { get; init; } = false;
}