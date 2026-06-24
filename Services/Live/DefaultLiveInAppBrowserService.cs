using Microsoft.Maui.ApplicationModel;

namespace PROXIMAMOP.Services.Live;

public sealed class DefaultLiveInAppBrowserService : ILiveInAppBrowserService
{
    public async Task OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Live URL is empty.");

        await Browser.Default.OpenAsync(
            url.Trim(),
            BrowserLaunchMode.SystemPreferred);
    }
}