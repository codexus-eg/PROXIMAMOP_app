using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class FeedMonitorService
{
    private readonly FeedService _feedService;
    private readonly INotificationService _notificationService;
    private readonly AppSettingsService _settingsService;

    private PeriodicTimer? _timer;
    private bool _started;
    private bool _isChecking;
    private bool _primed;

    public FeedMonitorService(
        FeedService feedService,
        INotificationService notificationService,
        AppSettingsService settingsService)
    {
        _feedService = feedService;
        _notificationService = notificationService;
        _settingsService = settingsService;
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        _ = Task.Run(async () =>
        {
            while (_timer != null && await _timer.WaitForNextTickAsync())
            {
                if (_isChecking)
                    continue;

                try
                {
                    _isChecking = true;
                    await CheckLatestAsync();
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    _isChecking = false;
                }
            }
        });
    }

    private async Task CheckLatestAsync()
    {
        var latest = await _feedService.GetLatestAsync();
        if (latest is null)
            return;

        var savedId = _settingsService.LastNotifiedFeedId;

        if (!_primed)
        {
            if (savedId == 0)
            {
                _settingsService.LastNotifiedFeedId = latest.Id;
            }

            _primed = true;
            return;
        }

        if (!_settingsService.FeedNotificationsEnabled)
            return;

        if (latest.Id <= savedId)
            return;

        var permissionGranted = await _notificationService.EnsurePermissionAsync();
        if (!permissionGranted)
            return;

        var title = "تحديث جديد";
        var body = BuildNotificationBody(latest);

        await _notificationService.ShowFeedNotificationAsync(title, body, latest.Id);

        _settingsService.LastNotifiedFeedId = latest.Id;
    }

    private static string BuildNotificationBody(FeedItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Text))
            return "يوجد تحديث جديد في الفيد";

        var text = item.Text.Replace("\r\n", "\n").Trim();

        if (text.Length > 120)
            return text[..120] + "...";

        return text;
    }
}