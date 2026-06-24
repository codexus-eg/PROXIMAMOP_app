using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class SettingsPage : ContentPage
{
    private readonly AppSettingsService _settingsService;
    private readonly INotificationService _notificationService;

    private bool _isInitializing;

    public SettingsPage()
    {
        InitializeComponent();

        _settingsService =
            ServiceHelper.GetService<AppSettingsService>()
            ?? new AppSettingsService();

        _notificationService =
            ServiceHelper.GetService<INotificationService>()
            ?? new DefaultNotificationService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isInitializing = true;

        FeedNotificationsSwitch.IsToggled =
            _settingsService.FeedNotificationsEnabled;

        UpdateStatusLabel(
            _settingsService.FeedNotificationsEnabled);

        _isInitializing = false;
    }

    private async void OnFeedNotificationsToggled(
        object sender,
        ToggledEventArgs e)
    {
        if (_isInitializing)
            return;

        if (e.Value)
        {
            var granted =
                await _notificationService.EnsurePermissionAsync();

            if (!granted)
            {
                FeedNotificationsSwitch.IsToggled = false;

                _settingsService.FeedNotificationsEnabled = false;

                UpdateStatusLabel(false);

                await DisplayAlert(
                    "تنبيه",
                    "لم يتم منح إذن الإشعارات.",
                    "OK");

                return;
            }

            _settingsService.FeedNotificationsEnabled = true;

            UpdateStatusLabel(true);
        }
        else
        {
            _settingsService.FeedNotificationsEnabled = false;

            UpdateStatusLabel(false);
        }
    }

    private void UpdateStatusLabel(bool enabled)
    {
        FeedNotificationsStatusLabel.Text =
            enabled
            ? "الحالة: مفعلة"
            : "الحالة: متوقفة";
    }

    private async void OnMyProfileClicked(
        object sender,
        EventArgs e)
    {
        await Navigation.PushAsync(new MyProfilePage());
    }

    private async void OnBackClicked(
        object sender,
        EventArgs e)
    {
        try
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            await Shell.Current.GoToAsync("//");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Error",
                ex.Message,
                "OK");
        }
    }
}