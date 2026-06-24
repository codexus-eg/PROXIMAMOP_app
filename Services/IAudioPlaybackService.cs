namespace PROXIMAMOP.Services;

public interface IAudioPlaybackService
{
    Task PlayAsync(string url, CancellationToken cancellationToken = default);
    Task StopAsync();
}