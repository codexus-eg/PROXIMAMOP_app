namespace PROXIMAMOP.Services.Live;

public interface ILiveInAppBrowserService
{
    Task OpenAsync(string url, CancellationToken cancellationToken = default);
}