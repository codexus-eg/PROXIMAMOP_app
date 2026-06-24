using PROXIMAMOP;

namespace PROXIMAMOP.Pages;

public partial class ServicesPage : ContentPage
{
    public ServicesPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnCopyTradesTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new CopyTradesPage());
    }

    private async void OnSednaTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new SednaPage());
    }

    // ✅ LOCMAP
    private async void OnLocmapTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new LocmapPage());
    }

    private async void OnLiquidityTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new LiquidityHeatmapPage());
    }

    // ✅ GREL90
    private async void OnGrel90Tapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new Grel90Page());
    }

    // ✅ FLOW90
    private async void OnFlow90Tapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new Flow90Page());
    }

    private async void OnMarketsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new MarketServicePage());
    }
}