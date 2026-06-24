namespace PROXIMAMOP.Features.Live;

public static class LiveConfig
{
    public const string ApiBase = "https://streamflowapp.com";
    public const string TokenEndpoint = ApiBase + "/getToken";
    public const string StartSessionEndpoint = ApiBase + "/live/sessions/start";
    public const string StopSessionEndpoint = ApiBase + "/live/sessions/stop";

    public const string HtmlUrl = "https://streamflowapp.com/live.html";
    public const string LiveKitUrl = "wss://live.streamflowapp.com";

    public const string DefaultRoomName = "test-room";
    public const string DefaultRequestedRole = "master";
}