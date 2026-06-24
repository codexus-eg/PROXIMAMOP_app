public class ExpiredPage : ContentPage
{
    public ExpiredPage()
    {
        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "الاشتراك منتهي ❌" },
                new Button
                {
                    Text = "تواصل معنا",
                    Command = new Command(async () =>
                    {
                        await Launcher.OpenAsync("https://t.me/YOUR_TELEGRAM");
                    })
                }
            }
        };
    }
}