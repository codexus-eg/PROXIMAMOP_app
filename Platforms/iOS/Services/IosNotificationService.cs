using System.Threading.Tasks;
using UserNotifications;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Platforms.iOS.Services;

public class IosNotificationService : INotificationService
{
    public async Task<bool> EnsurePermissionAsync()
    {
        var center = UNUserNotificationCenter.Current;
        var settings = await center.GetNotificationSettingsAsync();

        if (settings.AuthorizationStatus == UNAuthorizationStatus.Authorized ||
            settings.AuthorizationStatus == UNAuthorizationStatus.Provisional ||
            settings.AuthorizationStatus == UNAuthorizationStatus.Ephemeral)
        {
            return true;
        }

        var result = await center.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert |
            UNAuthorizationOptions.Badge |
            UNAuthorizationOptions.Sound);

        return result.Item1;
    }

    public async Task ShowFeedNotificationAsync(string title, string body, int notificationId)
    {
        var hasPermission = await EnsurePermissionAsync();
        if (!hasPermission)
            return;

        var content = new UNMutableNotificationContent
        {
            Title = title ?? string.Empty,
            Body = body ?? string.Empty,
            Sound = UNNotificationSound.Default
        };

        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);

        var request = UNNotificationRequest.FromIdentifier(
            notificationId.ToString(),
            content,
            trigger);

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
    }
}