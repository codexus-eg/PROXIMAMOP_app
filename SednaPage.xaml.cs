namespace PROXIMAMOP.Pages;

public partial class SednaPage : ContentPage
{
    private const string YoutubeUrl = "https://youtu.be/ebs2z9gBOm8?si=pOhVqApntftt-yGj";

    public SednaPage()
    {
        InitializeComponent();
    }

    private async void OnWatchVideoClicked(object sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync(YoutubeUrl);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnSubscribeClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SednaSubscribePage());
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}