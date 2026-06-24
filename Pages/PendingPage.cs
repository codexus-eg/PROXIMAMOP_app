using Microsoft.Maui.Storage;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public class PendingPage : ContentPage
{
    private readonly ActivationService _service = new();
    private readonly ChatService _chat = new();

    private readonly Label _statusLabel;
    private readonly Button _refreshButton;

    public PendingPage()
    {
        Title = "حالة الطلب";
        BackgroundColor = Color.FromArgb("#0B0B16");

        _statusLabel = new Label
        {
            Text = "طلبك قيد المراجعة ⏳",
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Colors.White,
            FontSize = 18
        };

        _refreshButton = new Button
        {
            Text = "تحديث الحالة",
            BackgroundColor = Color.FromArgb("#6D47D9"),
            TextColor = Colors.White,
            HeightRequest = 48,
            CornerRadius = 12,
            HorizontalOptions = LayoutOptions.Fill
        };

        _refreshButton.Clicked += OnRefreshClicked;

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _statusLabel,
                _refreshButton
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshStatusAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            _refreshButton.IsEnabled = false;
            _refreshButton.Text = "جاري التحقق...";

            var deviceId = _chat.GetOrCreateDeviceId();

            var me = await _chat.GetMeAsync(deviceId);
            if (me is null)
            {
                me = await _chat.RegisterOrUpdateAsync(deviceId, "User");
            }

            if (me is null)
            {
                await DisplayAlert("خطأ", "تعذر جلب بيانات المستخدم.", "حسناً");
                return;
            }

            var result = await _service.CheckStatusAsync(me.Id, deviceId);

            if (result is null)
            {
                await DisplayAlert("خطأ", "تعذر الاتصال بسيرفر التفعيل.", "حسناً");
                return;
            }

            if (result.CanEnterApp)
            {
                Preferences.Set("IsLoggedIn", true);
                Preferences.Set("IsActivated", true);
                Preferences.Set("UserId", me.Id);
                Preferences.Set("DeviceId", deviceId);

                if (result.ExpireAtUtc.HasValue)
                    Preferences.Set("ActivationExpireAtUtc", result.ExpireAtUtc.Value.ToUniversalTime().ToString("O"));

                Application.Current!.Windows[0].Page = new AppShell();
                return;
            }

            switch (result.Status?.Trim())
            {
                case "Pending":
                    _statusLabel.Text = "طلبك قيد المراجعة ⏳";
                    break;

                case "Rejected":
                case "Expired":
                case "Suspended":
                    Preferences.Set("IsActivated", false);
                    Application.Current!.Windows[0].Page = new NavigationPage(new ExpiredPage());
                    break;

                default:
                    Preferences.Set("IsActivated", false);
                    Application.Current!.Windows[0].Page = new NavigationPage(new ActivationGatePage());
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "حسناً");
        }
        finally
        {
            _refreshButton.IsEnabled = true;
            _refreshButton.Text = "تحديث الحالة";
        }
    }
}