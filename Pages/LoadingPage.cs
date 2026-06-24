using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public class LoadingPage : ContentPage
{
    private readonly ActivationService _service = new();
    private readonly ChatService _chat = new();

    public LoadingPage()
    {
        BackgroundColor = Color.FromArgb("#0B0B16");

        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator
                {
                    IsRunning = true,
                    Color = Colors.White,
                    WidthRequest = 50,
                    HeightRequest = 50
                },
                new Label
                {
                    Text = "جاري التحقق...",
                    TextColor = Colors.White,
                    Margin = new Thickness(0, 10, 0, 0),
                    HorizontalOptions = LayoutOptions.Center
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CheckAsync();
    }

    private async Task CheckAsync()
    {
        try
        {
            var deviceId = _chat.GetOrCreateDeviceId();

            var me = await _chat.GetMeAsync(deviceId);
            if (me is null)
            {
                me = await _chat.RegisterOrUpdateAsync(deviceId, "User");
            }

            if (me is null)
            {
                await DisplayAlert("خطأ", "تعذر إنشاء أو جلب بيانات المستخدم.", "حسناً");
                Application.Current!.Windows[0].Page = new NavigationPage(new ActivationGatePage());
                return;
            }

            var result = await _service.CheckStatusAsync(me.Id, deviceId);

            if (result is null)
            {
                await DisplayAlert("خطأ", "تعذر الاتصال بسيرفر التفعيل.", "حسناً");
                Application.Current!.Windows[0].Page = new NavigationPage(new ActivationGatePage());
                return;
            }

            if (result.CanEnterApp)
            {
                Application.Current!.Windows[0].Page = new AppShell();
                return;
            }

            switch (result.Status?.Trim())
            {
                case "Pending":
                    Application.Current!.Windows[0].Page = new NavigationPage(new PendingPage());
                    break;

                case "Rejected":
                case "Expired":
                case "Suspended":
                    Application.Current!.Windows[0].Page = new NavigationPage(new ExpiredPage());
                    break;

                default:
                    Application.Current!.Windows[0].Page = new NavigationPage(new ActivationGatePage());
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "حسناً");
            Application.Current!.Windows[0].Page = new NavigationPage(new ActivationGatePage());
        }
    }
}