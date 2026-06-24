using System.Text;
using System.Text.Json;

namespace PROXIMAMOP.Pages;

public partial class LiquidityHeatmapPage : ContentPage
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private PeriodicTimer? _timer;
    private bool _active;

    private const string Url = "http://195.3.223.134:6060/api/heatmap?symbol=XAUUSD";

    public LiquidityHeatmapPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _active = true;
        await LoadHtmlAsync();
        StartTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _active = false;
        _timer?.Dispose();
        _timer = null;
    }

    private async Task LoadHtmlAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("liquidity_heatmap.html");
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        HeatmapWebView.Source = new HtmlWebViewSource
        {
            Html = html
        };

        await Task.Delay(800);
        await RefreshAsync();
    }

    private void StartTimer()
    {
        if (_timer != null)
            return;

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        _ = Task.Run(async () =>
        {
            while (_active && _timer != null && await _timer.WaitForNextTickAsync())
            {
                await RefreshAsync();
            }
        });
    }

    private async Task RefreshAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(Url);

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("ok", out var okElement)
                && okElement.ValueKind == JsonValueKind.False)
            {
                await SetMessageAsync("No liquidity data yet");
                return;
            }

            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HeatmapWebView.EvaluateJavaScriptAsync($"setHeatmap('{base64}');");
            });
        }
        catch (Exception ex)
        {
            await SetMessageAsync("Error: " + ex.Message.Replace("'", ""));
        }
    }

    private async Task SetMessageAsync(string message)
    {
        var safe = message.Replace("\\", "\\\\").Replace("'", "\\'");

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await HeatmapWebView.EvaluateJavaScriptAsync($"showMessage('{safe}');");
        });
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}