namespace PROXIMAMOP.Pages;

public class ActivationGatePage : ContentPage
{
    public ActivationGatePage()
    {
        Title = "التفعيل";
        BackgroundColor = Color.FromArgb("#0B0B16");

        var titleLabel = new Label
        {
            Text = "التطبيق بحاجة الى تفعيل 🔒",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        var descLabel = new Label
        {
            Text = "تكلفة تفعيل التطبيق 200$ بأستثناء بعض الخدمات داخل التطبيق.",
            FontSize = 14,
            TextColor = Color.FromArgb("#C9C9D6"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var startButton = new Button
        {
            Text = "ابدأ التفعيل",
            BackgroundColor = Color.FromArgb("#6D47D9"),
            TextColor = Colors.White,
            HeightRequest = 50,
            CornerRadius = 12
        };

        startButton.Clicked += async (s, e) =>
        {
            await Navigation.PushAsync(new RegisterPage());
        };

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                titleLabel,
                descLabel,
                startButton
            }
        };
    }
}