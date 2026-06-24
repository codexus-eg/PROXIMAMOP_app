using Foundation;
using Microsoft.Maui.Storage;
using UIKit;
using UserNotifications;

namespace PROXIMAMOP;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate, IUNUserNotificationCenterDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        ConfigureNotifications(application);

        return result;
    }

    private void ConfigureNotifications(UIApplication application)
    {
        UNUserNotificationCenter.Current.Delegate = this;

        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert |
            UNAuthorizationOptions.Badge |
            UNAuthorizationOptions.Sound,
            (granted, error) =>
            {
                if (error is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"[iOS Notifications] Authorization error: {error.LocalizedDescription}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[iOS Notifications] Permission granted: {granted}");

                if (granted)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        application.RegisterForRemoteNotifications();
                    });
                }
            });
    }

    [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
    public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
    {
        var tokenBytes = deviceToken.ToArray();
        var token = BitConverter.ToString(tokenBytes).Replace("-", string.Empty);

        Preferences.Default.Set("ios_push_token", token);

        System.Diagnostics.Debug.WriteLine($"[iOS Notifications] Device Token: {token}");
    }

    [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
    public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
    {
        System.Diagnostics.Debug.WriteLine($"[iOS Notifications] Failed to register: {error.LocalizedDescription}");
    }

    [Export("userNotificationCenter:willPresentNotification:withCompletionHandler:")]
    public void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        System.Diagnostics.Debug.WriteLine("[iOS Notifications] Notification received while app is in foreground.");

        completionHandler(
            UNNotificationPresentationOptions.Sound |
            UNNotificationPresentationOptions.Badge |
            UNNotificationPresentationOptions.Banner |
            UNNotificationPresentationOptions.List);
    }

    [Export("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:")]
    public void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        try
        {
            var userInfo = response.Notification.Request.Content.UserInfo;

            if (userInfo is not null)
            {
                foreach (var item in userInfo)
                {
                    System.Diagnostics.Debug.WriteLine($"[iOS Notifications] Payload: {item.Key} = {item.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[iOS Notifications] Response handling error: {ex}");
        }
        finally
        {
            completionHandler();
        }
    }
}