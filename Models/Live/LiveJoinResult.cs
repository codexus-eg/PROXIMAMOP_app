namespace PROXIMAMOP.Models.Live;

public sealed class LiveJoinResult
{
    public bool IsSuccess { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public string JoinUrl { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public string RoomName { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string LiveKitUrl { get; private set; } = string.Empty;
    public string HtmlUrl { get; private set; } = string.Empty;

    public bool CanMic { get; private set; }
    public bool CanCamera { get; private set; }
    public bool CanScreen { get; private set; }
    public bool CanPublish { get; private set; }

    public static LiveJoinResult Success(
        string joinUrl,
        string token,
        string roomName,
        string userId,
        string displayName,
        string role,
        string liveKitUrl,
        string htmlUrl,
        bool canMic,
        bool canCamera,
        bool canScreen,
        bool canPublish)
    {
        return new LiveJoinResult
        {
            IsSuccess = true,
            JoinUrl = joinUrl,
            Token = token,
            RoomName = roomName,
            UserId = userId,
            DisplayName = displayName,
            Role = role,
            LiveKitUrl = liveKitUrl,
            HtmlUrl = htmlUrl,
            CanMic = canMic,
            CanCamera = canCamera,
            CanScreen = canScreen,
            CanPublish = canPublish
        };
    }

    public static LiveJoinResult Fail(string errorMessage)
    {
        return new LiveJoinResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage ?? "Unknown error."
        };
    }
}