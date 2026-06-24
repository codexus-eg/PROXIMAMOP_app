#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Platforms.Android.Services;

public class AndroidNotificationService : INotificationService
{
    private const string ChannelId = "feed_updates_channel";
    private const string ChannelName = "Feed Updates";

    private readonly Context _context;

    public AndroidNotificationService()
    {
        _context = global::Android.App.Application.Context;
        CreateNotificationChannel();
    }

    public async Task<bool> EnsurePermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status == PermissionStatus.Granted)
                return true;

            status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }

        return true;
    }

    public Task ShowFeedNotificationAsync(string title, string body, int notificationId)
    {
        var intent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName!);
        intent?.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop | ActivityFlags.NewTask);

        PendingIntent? pendingIntent = null;

        if (intent != null)
        {
            pendingIntent = PendingIntent.GetActivity(
                _context,
                notificationId,
                intent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        }

        var builder = new NotificationCompat.Builder(_context, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetPriority((int)NotificationPriority.High)
            .SetAutoCancel(true);

        if (pendingIntent != null)
            builder.SetContentIntent(pendingIntent);

        NotificationManagerCompat.From(_context).Notify(notificationId, builder.Build());

        return Task.CompletedTask;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            ChannelName,
            NotificationImportance.High)
        {
            Description = "Notifications for new feed updates"
        };

        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(channel);
    }
}
#endif