namespace PROXIMAMOP.Services;

public interface INotificationService
{
    Task<bool> EnsurePermissionAsync();
    Task ShowFeedNotificationAsync(string title, string body, int notificationId);
}