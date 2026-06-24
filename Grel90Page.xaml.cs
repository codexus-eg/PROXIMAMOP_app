using System.Text;

namespace PROXIMAMOP.Pages;

public partial class Grel90Page : ContentPage
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private PeriodicTimer? _timer;
    private bool _active;
    private bool _htmlLoaded;

    private string _timeframe = "M5";

    private const string BaseUrl = "http://195.3.223.134:6060/api/hvgrid?symbol=XAUUSD&timeframe=";

    public Grel90Page()
    {
        InitializeComponent();
        SetActiveButton();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _active = true;

        if (!_htmlLoaded)
        {
            await LoadHtmlAsync();
            _htmlLoaded = true;
        }

        await RefreshAsync();
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
        using var stream = await FileSystem.OpenAppPackageFileAsync("grel90.html");
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        GrelWebView.Source = new HtmlWebViewSource
        {
            Html = html
        };

        await Task.Delay(700);
    }

    private void StartTimer()
    {
        if (_timer is not null)
            return;

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        _ = Task.Run(async () =>
        {
            while (_active && _timer is not null && await _timer.WaitForNextTickAsync())
            {
                await RefreshAsync();
            }
        });
    }

    private async Task RefreshAsync()
    {
        if (!_active)
            return;

        try
        {
            var url = BaseUrl + _timeframe;
            var json = await _http.GetStringAsync(url);

            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await GrelWebView.EvaluateJavaScriptAsync($"setGrel90('{base64}', '{_timeframe}');");
            });
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Replace("\\", "\\\\").Replace("'", "\\'");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await GrelWebView.EvaluateJavaScriptAsync($"showMessage('Connection error: {msg}');");
            });
        }
    }

    private async void OnScalpingClicked(object sender, EventArgs e)
    {
        _timeframe = "M5";
        SetActiveButton();
        await RefreshAsync();
    }

    private async void OnSwingClicked(object sender, EventArgs e)
    {
        _timeframe = "M30";
        SetActiveButton();
        await RefreshAsync();
    }

    private void SetActiveButton()
    {
        var activeBg = Color.FromArgb("#C8A44D");
        var activeText = Color.FromArgb("#101010");

        var normalBg = Color.FromArgb("#1A1114");
        var normalText = Colors.White;

        BtnScalping.BackgroundColor = _timeframe == "M5" ? activeBg : normalBg;
        BtnScalping.TextColor = _timeframe == "M5" ? activeText : normalText;
        BtnScalping.BorderColor = Color.FromArgb("#C8A44D");
        BtnScalping.BorderWidth = 1;

        BtnSwing.BackgroundColor = _timeframe == "M30" ? activeBg : normalBg;
        BtnSwing.TextColor = _timeframe == "M30" ? activeText : normalText;
        BtnSwing.BorderColor = Color.FromArgb("#C8A44D");
        BtnSwing.BorderWidth = 1;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}