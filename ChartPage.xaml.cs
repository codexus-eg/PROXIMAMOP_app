using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class ChartPage : ContentPage
{
    private readonly ServerMarketService _marketService = new();
    private readonly LevelsService _levelsService = new();
    private readonly IndicatorLevelsService _indicatorLevelsService = new();
    private readonly CandleVelocityService _candleVelocityService = new();

    private readonly SemaphoreSlim _renderLock = new(1, 1);

    private PeriodicTimer? _liveTimer;
    private PeriodicTimer? _levelsTimer;
    private PeriodicTimer? _velocityTimer;

    private CancellationTokenSource? _pageCts;
    private TaskCompletionSource<bool>? _webViewLoadedTcs;

    private bool _chartReady;
    private bool _htmlLoaded;
    private bool _isPageActive;
    private bool _isSwitchingTimeframe;
    private bool _velocityVisible;

    private string _currentTimeframe = "M1";
    private long _lastCandleBucket = 0;

    public ChartPage()
    {
        InitializeComponent();

        ChartWebView.Navigated += ChartWebView_Navigated;

        SetActiveTimeframeButton(_currentTimeframe);
        SetLoading(false);
        SetVelocityVisible(false);
    }

    private void ChartWebView_Navigated(object? sender, WebNavigatedEventArgs e)
    {
        _webViewLoadedTcs?.TrySetResult(e.Result == WebNavigationResult.Success);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isPageActive = true;

        _pageCts?.Cancel();
        _pageCts = new CancellationTokenSource();

        try
        {
            SetLoading(true);

            if (!_htmlLoaded)
            {
                await LoadChartHtmlAsync();
                _htmlLoaded = true;
            }

            await EnsureChartReadyAsync();
            await RefreshChartAndObjectsAsync(_currentTimeframe, autoFit: true);

            StartLiveTimer();
            StartLevelsTimer();

            if (_velocityVisible)
                StartVelocityTimer();
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

        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pageCts = null;

        _liveTimer?.Dispose();
        _liveTimer = null;

        _levelsTimer?.Dispose();
        _levelsTimer = null;

        _velocityTimer?.Dispose();
        _velocityTimer = null;
    }

    private void SetLoading(bool isVisible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingOverlay.IsVisible = isVisible;
        });
    }

    private void SetVelocityVisible(bool visible)
    {
        _velocityVisible = visible;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            VelocityCard.IsVisible = visible;

            BtnVelocity.BackgroundColor = visible
                ? Color.FromArgb("#FFD700")
                : Color.FromArgb("#1A1114");

            BtnVelocity.TextColor = visible
                ? Color.FromArgb("#101010")
                : Color.FromArgb("#FFD700");
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
            BtnVelocity.IsEnabled = isEnabled;
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

            activeButton.BackgroundColor = Color.FromArgb("#7C3AED");
            activeButton.TextColor = Colors.White;
            activeButton.BorderColor = Color.FromArgb("#C4B5FD");
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

    private async Task LoadChartHtmlAsync()
    {
        _webViewLoadedTcs = new TaskCompletionSource<bool>();

        using var stream = await FileSystem.OpenAppPackageFileAsync("gold_chart.html");
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
            throw new Exception("WebView failed to navigate to gold_chart.html");
    }

    private async Task EnsureChartReadyAsync()
    {
        if (_chartReady)
            return;

        await Task.Delay(1200);

        for (var i = 0; i < 20; i++)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ChartWebView.EvaluateJavaScriptAsync("typeof setCandles === 'function'");
                });

                _chartReady = true;
                return;
            }
            catch
            {
                await Task.Delay(300);
            }
        }

        _chartReady = false;
    }

    private async Task<bool> SafeEvalJsAsync(string script)
    {
        if (!_isPageActive)
            return false;

        for (var i = 0; i < 10; i++)
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
                await Task.Delay(250);
            }
        }

        return false;
    }

    private async Task RefreshChartAndObjectsAsync(string timeframe, bool autoFit)
    {
        if (!_isPageActive)
            return;

        await EnsureChartReadyAsync();

        if (!_chartReady)
            return;

        await _renderLock.WaitAsync();

        try
        {
            _isSwitchingTimeframe = true;
            _currentTimeframe = timeframe;

            SetLoading(true);
            SetTimeframeButtonsEnabled(false);
            SetActiveTimeframeButton(timeframe);

            var candles = await _marketService.GetCandlesAsync(timeframe);
            var candlesJson = JsonSerializer.Serialize(candles);
            var candlesBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(candlesJson));
            var timeframeSeconds = ServerMarketService.TimeframeToSeconds(timeframe);

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _lastCandleBucket = nowUnix / timeframeSeconds * timeframeSeconds;

            await SafeEvalJsAsync($"setTimeframeSeconds({timeframeSeconds});");
            await SafeEvalJsAsync($"setCandles('{candlesBase64}', {(autoFit ? "true" : "false")});");

            await RefreshObjectsOnlyAsync(autoFit);
            await RefreshIndicatorLevelsAsync(autoFit);

            if (_velocityVisible)
                await RefreshCandleVelocityAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REFRESH CHART ERROR: " + ex.Message);
        }
        finally
        {
            _isSwitchingTimeframe = false;
            SetTimeframeButtonsEnabled(true);
            SetLoading(false);
            _renderLock.Release();
        }
    }

    private async Task RefreshCandlesOnlyAsync()
    {
        if (!_isPageActive || !_chartReady)
            return;

        try
        {
            var candles = await _marketService.GetCandlesAsync(_currentTimeframe);
            var candlesJson = JsonSerializer.Serialize(candles);
            var candlesBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(candlesJson));

            await SafeEvalJsAsync($"setCandles('{candlesBase64}', false);");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REFRESH CANDLES ERROR: " + ex.Message);
        }
    }

    private async Task RefreshObjectsOnlyAsync(bool autoFit)
    {
        if (!_isPageActive)
            return;

        await EnsureChartReadyAsync();

        if (!_chartReady)
            return;

        try
        {
            var levelsPayload = await _levelsService.GetLevelsAsync();

            if (levelsPayload is null)
                return;

            var json = JsonSerializer.Serialize(levelsPayload.Value);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await SafeEvalJsAsync($"setObjects('{base64}', {(autoFit ? "true" : "false")});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REFRESH OBJECTS ERROR: " + ex.Message);
        }
    }

    private async Task RefreshIndicatorLevelsAsync(bool autoFit)
    {
        if (!_isPageActive)
            return;

        await EnsureChartReadyAsync();

        if (!_chartReady)
            return;

        try
        {
            var indicatorPayload = await _indicatorLevelsService.GetIndicatorLevelsAsync();

            if (indicatorPayload is null)
                return;

            var json = JsonSerializer.Serialize(indicatorPayload.Value);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await SafeEvalJsAsync($"setIndicatorObjects('{base64}', {(autoFit ? "true" : "false")});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REFRESH INDICATOR LEVELS ERROR: " + ex.Message);
        }
    }

    private void StartLiveTimer()
    {
        if (_liveTimer != null)
            return;

        _liveTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _ = Task.Run(async () =>
        {
            while (_isPageActive && _liveTimer != null && await _liveTimer.WaitForNextTickAsync())
            {
                if (!_chartReady || _isSwitchingTimeframe)
                    continue;

                if (_renderLock.CurrentCount == 0)
                    continue;

                try
                {
                    var timeframeSeconds = ServerMarketService.TimeframeToSeconds(_currentTimeframe);
                    var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var bucket = unix / timeframeSeconds * timeframeSeconds;

                    if (_lastCandleBucket == 0)
                        _lastCandleBucket = bucket;

                    if (bucket > _lastCandleBucket)
                    {
                        _lastCandleBucket = bucket;
                        await RefreshCandlesOnlyAsync();
                        continue;
                    }

                    var price = await _marketService.GetLatestPriceAsync();

                    if (price is null)
                        continue;

                    var priceText = price.Value.ToString(CultureInfo.InvariantCulture);
                    await SafeEvalJsAsync($"updateLivePrice({priceText}, {unix});");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("LIVE TIMER ERROR: " + ex.Message);
                }
            }
        });
    }

    private void StartLevelsTimer()
    {
        if (_levelsTimer != null)
            return;

        _levelsTimer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        _ = Task.Run(async () =>
        {
            while (_isPageActive && _levelsTimer != null && await _levelsTimer.WaitForNextTickAsync())
            {
                if (!_chartReady || _isSwitchingTimeframe)
                    continue;

                if (_renderLock.CurrentCount == 0)
                    continue;

                try
                {
                    await RefreshObjectsOnlyAsync(false);
                    await RefreshIndicatorLevelsAsync(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("LEVELS TIMER ERROR: " + ex.Message);
                }
            }
        });
    }

    private void StartVelocityTimer()
    {
        if (_velocityTimer != null)
            return;

        _velocityTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _ = Task.Run(async () =>
        {
            while (_isPageActive && _velocityTimer != null && await _velocityTimer.WaitForNextTickAsync())
            {
                if (!_velocityVisible)
                    continue;

                await RefreshCandleVelocityAsync();
            }
        });
    }

    private async Task RefreshCandleVelocityAsync()
    {
        if (!_isPageActive || !_velocityVisible)
            return;

        try
        {
            var data = await _candleVelocityService.GetAsync("XAUUSD", _currentTimeframe);

            if (data is null || data.current is null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    VelocityStatusLabel.Text = "Velocity offline";
                });

                return;
            }

            var c = data.current;
            var p = data.panels;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                VelocityTfLabel.Text = _currentTimeframe;

                VelocityStatusLabel.Text = data.price > 0
                    ? $"Live price: {data.price.ToString("0.00", CultureInfo.InvariantCulture)}"
                    : "Live data connected";

                CvMaxLabel.Text = c.maxSpeed.ToString("0.00", CultureInfo.InvariantCulture);
                CvPrevLabel.Text = c.prevMax.ToString("0.00", CultureInfo.InvariantCulture);
                CvAvgLabel.Text = c.avgSpeed.ToString("0.00", CultureInfo.InvariantCulture);
                CvTicksLabel.Text = c.ticks.ToString(CultureInfo.InvariantCulture);
                CvTpsLabel.Text = c.ticksPerSecond.ToString("0.00", CultureInfo.InvariantCulture);

                CvBuyLabel.Text = c.buyLop.ToString(CultureInfo.InvariantCulture);
                CvSellLabel.Text = c.sellLop.ToString(CultureInfo.InvariantCulture);

                CvDiffLabel.Text = c.lopDiff > 0
                    ? $"+{c.lopDiff}"
                    : c.lopDiff.ToString(CultureInfo.InvariantCulture);

                CvBuyVolLabel.Text = c.buyVolSpeed.ToString("0.00", CultureInfo.InvariantCulture);
                CvSellVolLabel.Text = c.sellVolSpeed.ToString("0.00", CultureInfo.InvariantCulture);
                CvVolDiffLabel.Text = c.volDiffSpeed.ToString("0.00", CultureInfo.InvariantCulture);

                CvDiffLabel.TextColor = c.lopDiff switch
                {
                    > 0 => Color.FromArgb("#00FF88"),
                    < 0 => Color.FromArgb("#FF4D4D"),
                    _ => Color.FromArgb("#FFD700")
                };

                CvLopSideLabel.Text = p.lopSide;
                CvLopSideLabel.TextColor = p.lopSide switch
                {
                    "BUY" => Color.FromArgb("#00FF88"),
                    "SELL" => Color.FromArgb("#FF4D4D"),
                    _ => Color.FromArgb("#FFD700")
                };

                SetSpeedRows(p);
                SetTickRows(p);
                SetVolumeRows(p);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("VELOCITY ERROR: " + ex.Message);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                VelocityStatusLabel.Text = "Velocity offline";
            });
        }
    }

    private void SetSpeedRows(CandleVelocityPanels p)
    {
        CvSpeedRow1.Text = FormatSpeedRow(p, 0);
        CvSpeedRow2.Text = FormatSpeedRow(p, 1);
        CvSpeedRow3.Text = FormatSpeedRow(p, 2);
        CvSpeedRow4.Text = FormatSpeedRow(p, 3);
    }

    private void SetTickRows(CandleVelocityPanels p)
    {
        CvTicksRow1.Text = FormatTickRow(p, 0);
        CvTicksRow2.Text = FormatTickRow(p, 1);
        CvTicksRow3.Text = FormatTickRow(p, 2);
        CvTicksRow4.Text = FormatTickRow(p, 3);
    }

    private void SetVolumeRows(CandleVelocityPanels p)
    {
        CvVolumeRow1.Text = FormatVolumeRow(p, 0);
        CvVolumeRow2.Text = FormatVolumeRow(p, 1);
        CvVolumeRow3.Text = FormatVolumeRow(p, 2);
        CvVolumeRow4.Text = FormatVolumeRow(p, 3);
    }

    private static string FormatSpeedRow(CandleVelocityPanels p, int i)
    {
        if (p.speed.Count <= i || p.speed[i].time <= 0)
            return $"#{i + 1} —";

        var x = p.speed[i];
        return $"#{x.index} | {UnixToTime(x.time)} | Max: {x.max:0.00} | Avg: {x.avg:0.00}";
    }

    private static string FormatTickRow(CandleVelocityPanels p, int i)
    {
        if (p.ticks.Count <= i || p.ticks[i].time <= 0)
            return $"#{i + 1} —";

        var x = p.ticks[i];
        return $"#{x.index} | {UnixToTime(x.time)} | Ticks: {x.ticks} | {x.ticksPerSecond:0.00} t/s";
    }

    private static string FormatVolumeRow(CandleVelocityPanels p, int i)
    {
        if (p.volume.Count <= i || p.volume[i].time <= 0)
            return $"#{i + 1} —";

        var x = p.volume[i];
        return $"#{x.index} | {UnixToTime(x.time)} | BuyV/s: {x.buyVolSpeed:0.00} | SellV/s: {x.sellVolSpeed:0.00} | Diff: {x.diffVolSpeed:0.00}";
    }

    private static string UnixToTime(long unix)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix)
                .ToLocalTime()
                .ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "--:--";
        }
    }

    private async void OnVelocityClicked(object sender, EventArgs e)
    {
        SetVelocityVisible(!_velocityVisible);

        if (_velocityVisible)
        {
            StartVelocityTimer();
            await RefreshCandleVelocityAsync();
        }
    }

    private async void OnTimeframeClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is null)
            return;

        if (_isSwitchingTimeframe)
            return;

        var timeframe = btn.CommandParameter.ToString()!;

        if (string.Equals(timeframe, _currentTimeframe, StringComparison.OrdinalIgnoreCase))
            return;

        await RefreshChartAndObjectsAsync(timeframe, autoFit: true);
    }
}