using System.Reflection;

namespace PROXIMAMOP.Pages;

public partial class Flow90Page : ContentPage
{
    private bool _loaded;

    public Flow90Page()
    {
        InitializeComponent();

        LoadHtml();
    }

    private async void LoadHtml()
    {
        try
        {
            LoadingOverlay.IsVisible = true;

            await Task.Delay(250);

            using var stream = await FileSystem.OpenAppPackageFileAsync("flow90.html");

            using var reader = new StreamReader(stream);

            string html = await reader.ReadToEndAsync();

            FlowWebView.Source = new HtmlWebViewSource
            {
                Html = html
            };

            await Task.Delay(600);

            _loaded = true;

            LoadingOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "FLOW90 ERROR",
                ex.ToString(),
                "OK");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
        });

        return true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
            return;

        try
        {
            await FlowWebView.EvaluateJavaScriptAsync(
                "showMessage('FLOW90 CONNECTED');");
        }
        catch
        {
        }
    }
}