namespace PROXIMAMOP.Services;

public class DefaultNotificationService : INotificationService
{
    public Task<bool> EnsurePermissionAsync()
    {
        return Task.FromResult(true);
    }

    public Task ShowFeedNotificationAsync(string title, string body, int notificationId)
    {
        return Task.CompletedTask;
    }
}
