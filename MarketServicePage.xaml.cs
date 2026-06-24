using System.Globalization;
using System.Text;
using System.Text.Json;
using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class MarketServicePage : ContentPage
{
    private readonly NewMarketServerService _marketService = new();
    private readonly NewMarketLevelsService _levelsService = new();

    private readonly SemaphoreSlim _renderLock = new(1, 1);

    private PeriodicTimer? _liveTimer;
    private PeriodicTimer? _levelsTimer;

    private TaskCompletionSource<bool>? _webViewLoadedTcs;

    private bool _chartReady;
    private bool _htmlLoaded;
    private bool _isPageActive;
    private bool _isSwitching;

    private int _refreshVersion = 0;

    private string _currentSymbol = "XAUUSD";
    private string _currentTimeframe = "M1";

    private static readonly string[] Symbols =
    {
        "XAUUSD",
        "EURUSD",
        "BTCUSD",
        "USTEC",
        "DJ30"
    };

    public MarketServicePage()
    {
        InitializeComponent();

        ChartWebView.Navigated += ChartWebView_Navigated;

        SetActiveTimeframeButton(_currentTimeframe);
        SetSymbolUi(_currentSymbol);
        SetLoading(false);
    }

    private void ChartWebView_Navigated(object? sender, WebNavigatedEventArgs e)
    {
        _webViewLoadedTcs?.TrySetResult(e.Result == WebNavigationResult.Success);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isPageActive = true;

        try
        {
            SetLoading(true);

            if (!_htmlLoaded)
            {
                await LoadChartHtmlAsync();
                _htmlLoaded = true;
            }

            await EnsureChartReadyAsync();

            await RefreshChartAndObjectsAsync(autoFit: true);

            StartLiveTimer();
            StartLevelsTimer();
        }
        finally
        {
            SetLoading(false);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _isPageActive = false;
        _refreshVersion++;

        _liveTimer?.Dispose();
        _liveTimer = null;

        _levelsTimer?.Dispose();
        _levelsTimer = null;
    }

    private async Task LoadChartHtmlAsync()
    {
        _webViewLoadedTcs = new TaskCompletionSource<bool>();

        using var stream = await FileSystem.OpenAppPackageFileAsync("market_chart.html");
        using var reader = new StreamReader(stream);

        var html = await reader.ReadToEndAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ChartWebView.Source = new HtmlWebViewSource
            {
                Html = html
            };
        });

        var loaded = await _webViewLoadedTcs.Task;

        if (!loaded)
            throw new Exception("WebView failed to navigate to market_chart.html");
    }

    private async Task EnsureChartReadyAsync()
    {
        if (_chartReady)
            return;

        await Task.Delay(1000);

        for (var i = 0; i < 25; i++)
        {
            try
            {
                var result = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await ChartWebView.EvaluateJavaScriptAsync(
                        "typeof setCandles === 'function'");
                });

                if (result?.Contains("true",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    _chartReady = true;
                    return;
                }
            }
            catch
            {
                await Task.Delay(250);
            }
        }

        _chartReady = true;
    }

    private async Task<bool> SafeEvalJsAsync(string script)
    {
        if (!_isPageActive)
            return false;

        for (var i = 0; i < 8; i++)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ChartWebView.EvaluateJavaScriptAsync(script);
                });
                return true;
            }
            catch
            {
                await Task.Delay(200);
            }
        }

        return false;
    }

    private async Task RefreshChartAndObjectsAsync(bool autoFit)
    {
        if (!_isPageActive)
            return;

        var myVersion = ++_refreshVersion;

        var symbol = _currentSymbol;
        var timeframe = _currentTimeframe;

        await EnsureChartReadyAsync();

        await _renderLock.WaitAsync();

        try
        {
            _isSwitching = true;

            SetLoading(true);

            SetTimeframeButtonsEnabled(false);

            SetActiveTimeframeButton(timeframe);

            SetSymbolUi(symbol);

            var candles =
                await _marketService.GetCandlesAsync(symbol, timeframe);

            if (!_isPageActive || myVersion != _refreshVersion)
                return;

            var candlesJson = JsonSerializer.Serialize(candles);

            var candlesBase64 =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(candlesJson));

            var timeframeSeconds =
                NewMarketServerService.TimeframeToSeconds(timeframe);

            await SafeEvalJsAsync(
                $"setTimeframeSeconds({timeframeSeconds});");

            if (!_isPageActive || myVersion != _refreshVersion)
                return;

            await SafeEvalJsAsync(
                $"setCandles('{candlesBase64}', {(autoFit ? "true" : "false")});");

            if (!_isPageActive || myVersion != _refreshVersion)
                return;

            await RefreshObjectsOnlyAsync(autoFit, myVersion, symbol);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "NEW MARKET REFRESH ERROR: " + ex.Message);
        }
        finally
        {
            _isSwitching = false;

            SetTimeframeButtonsEnabled(true);

            SetLoading(false);

            if (_renderLock.CurrentCount == 0)
                _renderLock.Release();
        }
    }

    private async Task RefreshObjectsOnlyAsync(bool autoFit)
    {
        var myVersion = _refreshVersion;

        var symbol = _currentSymbol;

        await RefreshObjectsOnlyAsync(autoFit, myVersion, symbol);
    }

    private async Task RefreshObjectsOnlyAsync(
        bool autoFit,
        int version,
        string symbol)
    {
        if (!_isPageActive || !_chartReady)
            return;

        try
        {
            var levelsPayload =
                await _levelsService.GetLevelsAsync(symbol);

            if (!_isPageActive || version != _refreshVersion)
                return;

            if (levelsPayload is null)
                return;

            var json =
                JsonSerializer.Serialize(levelsPayload.Value);

            var base64 =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(json));

            await SafeEvalJsAsync(
                $"setObjects('{base64}', {(autoFit ? "true" : "false")});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "NEW MARKET OBJECTS ERROR: " + ex.Message);
        }
    }

    private void StartLiveTimer()
    {
        if (_liveTimer != null)
            return;

        _liveTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(1));

        _ = Task.Run(async () =>
        {
        while (_isPageActive &&
               _liveTimer != null &&
               await _liveTimer.WaitForNextTickAsync())
        {
            if (!_chartReady || _isSwitching)
                continue;

            if (_renderLock.CurrentCount == 0)
                continue;

            var version = _refreshVersion;

            var symbol = _currentSymbol;

            try
            {
                var price =
                    await _marketService.GetLatestPriceAsync(symbol);
                    if (!_isPageActive || version != _refreshVersion)
                        continue;

                    if (price is null || price <= 0)
                        continue;

                    var unix =
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    var priceText =
                        price.Value.ToString(
                            CultureInfo.InvariantCulture);

                    await SafeEvalJsAsync(
                        $"updateLivePrice({priceText}, {unix});");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "NEW MARKET LIVE ERROR: " + ex.Message);
                }
            }
        });
    }

    private void StartLevelsTimer()
    {
        if (_levelsTimer != null)
            return;

        _levelsTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(3));

        _ = Task.Run(async () =>
        {
            while (_isPageActive &&
                   _levelsTimer != null &&
                   await _levelsTimer.WaitForNextTickAsync())
            {
                if (!_chartReady || _isSwitching)
                    continue;

                if (_renderLock.CurrentCount == 0)
                    continue;

                await RefreshObjectsOnlyAsync(autoFit: false);
            }
        });
    }

    private async void OnTimeframeClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is null)
            return;

        if (_isSwitching)
            return;

        var timeframe = btn.CommandParameter.ToString()!;

        if (string.Equals(
                timeframe,
                _currentTimeframe,
                StringComparison.OrdinalIgnoreCase))
            return;

        _currentTimeframe = timeframe;

        await RefreshChartAndObjectsAsync(autoFit: true);
    }

    private async void OnSymbolClicked(object sender, EventArgs e)
    {
        if (_isSwitching)
            return;

        var selected = await DisplayActionSheet(
            "Select Market",
            "Cancel",
            null,
            Symbols);

        if (string.IsNullOrWhiteSpace(selected) ||
            selected == "Cancel")
            return;

        if (string.Equals(
                selected,
                _currentSymbol,
                StringComparison.OrdinalIgnoreCase))
            return;

        _currentSymbol = selected;

        _refreshVersion++;

        await RefreshChartAndObjectsAsync(autoFit: true);
    }

    private void SetSymbolUi(string symbol)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SymbolButton.Text = symbol;
            MarketLabel.Text = symbol;
        });
    }

    private void SetLoading(bool isVisible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingOverlay.IsVisible = isVisible;
        });
    }

    private void SetTimeframeButtonsEnabled(bool isEnabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BtnM1.IsEnabled = isEnabled;
            BtnM5.IsEnabled = isEnabled;
            BtnM15.IsEnabled = isEnabled;
            BtnM30.IsEnabled = isEnabled;
            BtnH1.IsEnabled = isEnabled;
            BtnH4.IsEnabled = isEnabled;
            BtnD1.IsEnabled = isEnabled;
        });
    }

    private void SetActiveTimeframeButton(string timeframe)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
        ResetButtonStyle(BtnM1);
        ResetButtonStyle(BtnM5);
        ResetButtonStyle(BtnM15);
        ResetButtonStyle(BtnM30);
        ResetButtonStyle(BtnH1);
        ResetButtonStyle(BtnH4);
        ResetButtonStyle(BtnD1);
            var activeButton = timeframe switch
            {
                "M1" => BtnM1,
                "M5" => BtnM5,
                "M15" => BtnM15,
                "M30" => BtnM30,
                "H1" => BtnH1,
                "H4" => BtnH4,
                "D1" => BtnD1,
                _ => BtnM1
            };

            activeButton.BackgroundColor =
                Color.FromArgb("#7C3AED");

            activeButton.TextColor = Colors.White;

            activeButton.BorderColor =
                Color.FromArgb("#C4B5FD");

            activeButton.BorderWidth = 1;
        });
    }

    private static void ResetButtonStyle(Button button)
    {
        button.BackgroundColor = Color.FromArgb("#A78BFA");
        button.TextColor = Colors.White;
        button.BorderColor = Colors.Transparent;
        button.BorderWidth = 0;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}