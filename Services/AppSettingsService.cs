using Microsoft.Maui.Storage;

namespace PROXIMAMOP.Services;

public class AppSettingsService
{
    private const string FeedNotificationsEnabledKey = "feed_notifications_enabled";
    private const string LastNotifiedFeedIdKey = "last_notified_feed_id";

    public bool FeedNotificationsEnabled
    {
        get => Preferences.Default.Get(FeedNotificationsEnabledKey, true);
        set => Preferences.Default.Set(FeedNotificationsEnabledKey, value);
    }

    public int LastNotifiedFeedId
    {
        get => Preferences.Default.Get(LastNotifiedFeedIdKey, 0);
        set => Preferences.Default.Set(LastNotifiedFeedIdKey, value);
    }
}