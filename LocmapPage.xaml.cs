using PROXIMAMOP.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;


namespace PROXIMAMOP;

public partial class LocmapPage : ContentPage
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };
    private readonly LocmapIndicatorLevelsService _indicatorLevelsService;
    private readonly ObservableCollection<HeatmapApiModel> _levels = new();
    private readonly List<List<HeatmapApiModel>> _history = new();

    private readonly List<StoredLiquidityCell> _storedCells = new();

    private PeriodicTimer? _timer;

    private bool _active;
    private bool _loading;
    private bool _htmlLoaded;

    private double _livePrice;

    private const int MaxHistoryColumns = 90;
    private const int RowsCount = 320;
    private const int MaxLevelsPerColumn = 30;
    private readonly IndicatorLevelsService _indicatorLevels;
    private List<LocmapIndicatorLevel> _indicatorLevelsCache = new();
    private const double RedCellThreshold = 97.0;
    private const double TouchDistance = 0.35;
    private const double ExitDistance = 0.85;

    private const int StoredCellLifeTicks = 120;
    private const int TouchedLifeTicks = 28;

    private const string LocmapUrl =
        "http://195.3.223.136:7070/api/heatmap/grid?symbol=XAUUSD&minutes=30";

    private const string LiquidityUrl =
        "http://195.3.223.134:6060/api/heatmap?symbol=XAUUSD";

    public LocmapPage()
    {
        InitializeComponent();

        _indicatorLevelsService = new LocmapIndicatorLevelsService(_http);

        LevelsCollection.ItemsSource = _levels;
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

        await LoadAsync();
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
        using var stream = await FileSystem.OpenAppPackageFileAsync("locmap.html");
        using var reader = new StreamReader(stream);

        var html = await reader.ReadToEndAsync();

        HeatmapWebView.Source = new HtmlWebViewSource
        {
            Html = html
        };

        await Task.Delay(700);
    }

    private void StartTimer()
    {
        if (_timer != null)
            return;

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _ = Task.Run(async () =>
        {
            while (_active &&
                   _timer != null &&
                   await _timer.WaitForNextTickAsync())
            {
                await LoadAsync();
            }
        });
    }

    private async Task LoadAsync()
    {
        if (_loading)
            return;

        try
        {
            _loading = true;

            var locmapTask = _http.GetStringAsync(LocmapUrl);
            var liquidityTask = GetLiquiditySourceAsync();

            await Task.WhenAll(locmapTask, liquidityTask);

            var locmapJson = await locmapTask;
            var liquiditySource = await liquidityTask;

            try
            {
                _indicatorLevelsCache =
                    await _indicatorLevelsService.GetHv5MainLevelsAsync();
            }
            catch
            {
                _indicatorLevelsCache = new List<LocmapIndicatorLevel>();
            }

            
           
            var data = JsonSerializer.Deserialize<List<HeatmapApiModel>>(
                locmapJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (data == null || data.Count == 0)
                return;

            foreach (var item in data)
                item.BuildUi();

            var sorted = data
                .Where(x => x.Price > 0)
                .OrderByDescending(x => x.Price)
                .ToList();

            if (sorted.Count == 0)
                return;

            var priceStep = GetPriceStep(sorted);

            _history.Add(sorted);

            while (_history.Count > MaxHistoryColumns)
            {
                _history.RemoveAt(0);
                ShiftStoredCellsLeft();
            }

            var latestColumn = Math.Max(0, _history.Count - 1);
            UpdateStoredCellsFromLiquidity(liquiditySource, priceStep, latestColumn);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                UpdateStats(sorted);
                UpdateCards(sorted);
                await SendMatrixToHtmlAsync(sorted);
            });
        }
        catch
        {
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task<LiquidityHeatmapDto?> GetLiquiditySourceAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(LiquidityUrl);

            var dto = JsonSerializer.Deserialize<LiquidityHeatmapDto>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (dto == null)
                return null;

            if (dto.Price > 0)
                _livePrice = dto.Price;
            else if (dto.Bid > 0)
                _livePrice = dto.Bid;

            return dto;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateStats(List<HeatmapApiModel> data)
    {
        LevelsLabel.Text = data.Count.ToString();

        ContractsLabel.Text = data
            .Sum(x => x.Contracts)
            .ToString("0.0");

        var displayPrice = _livePrice > 0
            ? _livePrice
            : GetLastPrice(data);

        LastPriceLabel.Text = displayPrice > 0
            ? displayPrice.ToString("0.00")
            : "0.00";
    }

    private void UpdateCards(List<HeatmapApiModel> data)
    {
        for (int i = 0; i < data.Count; i++)
        {
            if (i < _levels.Count)
                _levels[i] = data[i];
            else
                _levels.Add(data[i]);
        }

        while (_levels.Count > data.Count)
            _levels.RemoveAt(_levels.Count - 1);
    }

    private async Task SendMatrixToHtmlAsync(List<HeatmapApiModel> latest)
    {
        if (!_htmlLoaded || _history.Count == 0)
            return;

        var matrix = BuildLocmapMatrix(latest);

        var json = JsonSerializer.Serialize(
            matrix,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        await HeatmapWebView.EvaluateJavaScriptAsync($"setHeatmap('{base64}');");
    }

    private LocmapMatrix BuildLocmapMatrix(List<HeatmapApiModel> latest)
    {
        var priceStep = GetPriceStep(latest);

        var livePrice = _livePrice > 0
            ? _livePrice
            : GetLastPrice(latest);

        if (livePrice <= 0)
            livePrice = latest.First().Price;

        var allPrices = _history
     .SelectMany(x => x)
     .Where(x => x.Price > 0)
     .Select(x => RoundToStep(x.Price, priceStep))
     .ToList();

        if (allPrices.Count == 0)
            allPrices.Add(RoundToStep(livePrice, priceStep));

        allPrices.Add(RoundToStep(livePrice, priceStep));

        foreach (var cell in _storedCells)
            allPrices.Add(RoundToStep(cell.Price, priceStep));

        foreach (var level in _indicatorLevelsCache)
        {
            if (level.Price > 0)
                allPrices.Add(RoundToStep(level.Price, priceStep));
        }

        var dataLo = allPrices.Min();
        var dataHi = allPrices.Max();

        var centerPrice = RoundToStep(livePrice, priceStep);

        var maxDistance = 60.0; // زيدها 60 إذا تريد أبعد
        var padding = 6 * priceStep;

        var lo = Math.Max(dataLo - padding, centerPrice - maxDistance);
        var hi = Math.Min(dataHi + padding, centerPrice + maxDistance);

        if (hi <= lo)
        {
            lo = centerPrice - maxDistance;
            hi = centerPrice + maxDistance;
        }

        var requiredRows = (int)Math.Ceiling((hi - lo) / priceStep) + 1;
        var rows = Math.Max(RowsCount, requiredRows);

        hi = lo + (rows - 1) * priceStep;

        var cols = _history.Count;

        var rawHeat = new double[rows * cols];
        var colVol = new double[cols];
        var acceptedScores = new List<double>();

        for (int c = 0; c < cols; c++)
        {
            var snapshot = _history[c];

            var scored = snapshot
                .Where(x => x.Price > 0)
                .Select(x => new
                {
                    Item = x,
                    Price = RoundToStep(x.Price, priceStep),
                    Score = CalculateLiquidityScore(x)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(60)
                .ToList();

            if (scored.Count == 0)
                continue;

            var localMax = scored.Max(x => x.Score);
            var localThreshold = localMax * 0.12;

            foreach (var level in scored)
            {
                if (level.Score < localThreshold)
                    continue;

                var row = (int)Math.Round((hi - level.Price) / priceStep);

                if (row < 0 || row >= rows)
                    continue;

                var index = row * cols + c;

                rawHeat[index] = Math.Max(rawHeat[index], level.Score);
                colVol[c] += Math.Max(0, level.Item.Contracts);

                acceptedScores.Add(level.Score);
            }
        }

        foreach (var level in _indicatorLevelsCache)
        {
            if (level.Price <= 0)
                continue;

            var roundedPrice = RoundToStep(level.Price, priceStep);
            var row = (int)Math.Round((hi - roundedPrice) / priceStep);

            if (row < 0 || row >= rows)
                continue;

            for (int c = 0; c < cols; c++)
            {
                var index = row * cols + c;

                rawHeat[index] = Math.Max(rawHeat[index], 999999);
                acceptedScores.Add(999999);
            }
        }

        var heat = NormalizeSparseHeat(rawHeat, acceptedScores);
        var zones = BuildStoredZones(rows, cols, hi, priceStep);

        return new LocmapMatrix
        {
            Rows = rows,
            Cols = cols,
            Heat = heat,
            Zones = zones,
            ColVol = colVol.Select(x => Math.Round(x, 1)).ToArray(),
            Lo = Math.Round(lo, 2),
            Hi = Math.Round(hi, 2),
            Price = Math.Round(livePrice, 2),
            Symbol = "XAUUSD"
        };
    }

    private void UpdateStoredCellsFromLiquidity(
    LiquidityHeatmapDto? source,
    double priceStep,
    int latestColumn)
    {
        var now = DateTime.UtcNow;
        var livePrice = _livePrice;

        for (int i = _storedCells.Count - 1; i >= 0; i--)
        {
            var cell = _storedCells[i];

            if (livePrice > 0)
            {
                var distance = Math.Abs(cell.Price - livePrice);

                if (!cell.Touched && distance <= TouchDistance)
                {
                    cell.Touched = true;
                    cell.Life = TouchedLifeTicks;
                }

                if (cell.Touched && distance > ExitDistance)
                {
                    _storedCells.RemoveAt(i);
                    continue;
                }
            }

            cell.Life--;

            if (cell.Life <= 0)
            {
                _storedCells.RemoveAt(i);
                continue;
            }

            cell.Strength *= 0.995;
        }

        if (source == null ||
            source.Rows <= 0 ||
            source.Cols <= 0 ||
            source.Heat == null ||
            source.Heat.Length < source.Rows * source.Cols ||
            source.Hi <= source.Lo)
        {
            return;
        }

        

        for (int r = 0; r < source.Rows; r++)
        {
            var t = 1.0 - r / Math.Max(1.0, source.Rows - 1.0);
            var price = source.Lo + (source.Hi - source.Lo) * t;
            var roundedPrice = RoundToStep(price, priceStep);

            for (int c = 0; c < source.Cols; c++)
            {
                var value = source.Heat[r * source.Cols + c];

                if (value < RedCellThreshold)
                    continue;

                AddOrRefreshStoredCell(
    roundedPrice,
    latestColumn,
    value,
    livePrice,
    now);
            }
        }

        MergeCloseCells();
    }

    private void AddOrRefreshStoredCell(
        double price,
        int column,
        double strength,
        double livePrice,
        DateTime now)
    {
        var existing = _storedCells.FirstOrDefault(x =>
            Math.Abs(x.Price - price) < 0.00001 &&
            Math.Abs(x.Column - column) <= 1);

        if (existing == null)
        {
            existing = new StoredLiquidityCell
            {
                Price = price,
                Column = column,
                Strength = strength,
                Touched = false,
                Life = StoredCellLifeTicks,
                LastSeen = now
            };

            _storedCells.Add(existing);
        }

        if (!existing.Touched)
        {
            existing.Strength = Math.Max(existing.Strength, strength);
            existing.Life = StoredCellLifeTicks;
            existing.LastSeen = now;

            if (livePrice > 0 &&
                Math.Abs(livePrice - price) <= TouchDistance)
            {
                existing.Touched = true;
                existing.Life = TouchedLifeTicks;
            }
        }
    }

    private void MergeCloseCells()
    {
        if (_storedCells.Count <= 1)
            return;

        var ordered = _storedCells
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Column)
            .ToList();

        _storedCells.Clear();

        foreach (var cell in ordered)
        {
            var existing = _storedCells.FirstOrDefault(x =>
                Math.Abs(x.Price - cell.Price) < 0.00001 &&
                Math.Abs(x.Column - cell.Column) <= 1 &&
                x.Touched == cell.Touched);

            if (existing == null)
            {
                _storedCells.Add(cell);
                continue;
            }

            existing.Strength = Math.Max(existing.Strength, cell.Strength);
            existing.Life = Math.Max(existing.Life, cell.Life);
            existing.LastSeen = existing.LastSeen > cell.LastSeen
                ? existing.LastSeen
                : cell.LastSeen;
        }

        while (_storedCells.Count > 600)
        {
            var weakest = _storedCells
                .OrderBy(x => x.Strength)
                .ThenBy(x => x.Life)
                .FirstOrDefault();

            if (weakest == null)
                break;

            _storedCells.Remove(weakest);
        }
    }
    private void ShiftStoredCellsLeft()
    {
        for (int i = _storedCells.Count - 1; i >= 0; i--)
        {
            _storedCells[i].Column--;

            if (_storedCells[i].Column < 0)
                _storedCells.RemoveAt(i);
        }
    }

    private double[] BuildStoredZones(
     int rows,
     int cols,
     double hi,
     double priceStep)
    {
        var zones = new double[rows * cols];

        if (cols <= 0)
            return zones;

        foreach (var cell in _storedCells)
        {
            var row = (int)Math.Round((hi - cell.Price) / priceStep);

            if (row < 0 || row >= rows)
                continue;

            var column = Math.Clamp(cell.Column, 0, cols - 1);

            var value = cell.Touched ? 101.0 : 96.0;

            var radius = cell.Touched ? 1 : 0;

            for (int c = column - radius; c <= column + radius; c++)
            {
                if (c < 0 || c >= cols)
                    continue;

                var index = row * cols + c;

                if (index >= 0 && index < zones.Length)
                    zones[index] = Math.Max(zones[index], value);
            }
        }

        return zones;
    }

    private static int ProjectSourceColumnToLocmapColumn(
        int sourceColumn,
        int sourceCols,
        int targetCols)
    {
        if (sourceCols <= 1 || targetCols <= 1)
            return 0;

        var ratio = sourceColumn / (double)(sourceCols - 1);
        return (int)Math.Round(ratio * (targetCols - 1));
    }

    private static double[] NormalizeSparseHeat(
        double[] rawHeat,
        List<double> scores)
    {
        var result = new double[rawHeat.Length];

        if (scores.Count == 0)
            return result;

        var ordered = scores
            .Where(x => x > 0)
            .OrderBy(x => x)
            .ToList();

        if (ordered.Count == 0)
            return result;

        var p50 = Percentile(ordered, 0.50);
        var p75 = Percentile(ordered, 0.75);
        var p90 = Percentile(ordered, 0.90);
        var p97 = Percentile(ordered, 0.97);

        if (p50 <= 0) p50 = ordered.First();
        if (p75 <= p50) p75 = p50 * 1.25;
        if (p90 <= p75) p90 = p75 * 1.25;
        if (p97 <= p90) p97 = p90 * 1.25;

        for (int i = 0; i < rawHeat.Length; i++)
        {
            var v = rawHeat[i];

            if (v <= 0)
                continue;

            double normalized;

            if (v <= p50)
                normalized = Map(v, 0, p50, 18, 34);
            else if (v <= p75)
                normalized = Map(v, p50, p75, 35, 52);
            else if (v <= p90)
                normalized = Map(v, p75, p90, 53, 70);
            else if (v <= p97)
                normalized = Map(v, p90, p97, 71, 84);
            else
                normalized = Map(v, p97, ordered.Last(), 85, 100);

            result[i] = Math.Clamp(Math.Round(normalized, 2), 0, 100);
        }

        return result;
    }

    private static double CalculateLiquidityScore(HeatmapApiModel x)
    {
        var score = 0.0;

        score += Math.Max(0, x.Heat) * 0.85;
        score += Math.Max(0, x.Contracts) * 5.20;
        score += Math.Abs(x.Delta) * 0.25;
        score += Math.Max(0, x.TotalTicks) * 0.60;
        score += Math.Max(0, x.Persistence) * 2.20;

        if (x.AggressiveBuy || x.AggressiveSell)
            score *= 1.10;

        if (x.Absorption)
            score *= 1.18;

        if (x.LiquiditySweep)
            score *= 1.24;

        if (x.TickBurst)
            score *= 1.12;

        return score;
    }

    private static double GetPriceStep(List<HeatmapApiModel> data)
    {
        var step = data
            .Where(x => x.PriceStep > 0)
            .Select(x => x.PriceStep)
            .FirstOrDefault();

        return step > 0 ? step : 0.5;
    }

    private static double GetLastPrice(List<HeatmapApiModel> data)
    {
        var lastPrice = data
            .Where(x => x.LastPrice > 0)
            .Select(x => x.LastPrice)
            .FirstOrDefault();

        return lastPrice > 0
            ? lastPrice
            : data.FirstOrDefault(x => x.Price > 0)?.Price ?? 0;
    }

    private static double RoundToStep(double price, double step)
    {
        return step <= 0
            ? price
            : Math.Round(price / step) * step;
    }

    private static double Percentile(List<double> ordered, double percentile)
    {
        if (ordered.Count == 0)
            return 0;

        if (ordered.Count == 1)
            return ordered[0];

        var position = (ordered.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
            return ordered[lower];

        var weight = position - lower;

        return ordered[lower] * (1 - weight) +
               ordered[upper] * weight;
    }

    private static double Map(
        double value,
        double fromMin,
        double fromMax,
        double toMin,
        double toMax)
    {
        if (fromMax <= fromMin)
            return toMin;

        var t = (value - fromMin) / (fromMax - fromMin);

        return toMin + t * (toMax - toMin);
    }

    private void OnCardsClicked(object sender, EventArgs e)
    {
        LevelsCollection.IsVisible = true;
        HeatmapContainer.IsVisible = false;

        CardsButton.BackgroundColor = Color.FromArgb("#C8A44D");
        CardsButton.TextColor = Color.FromArgb("#111827");

        HeatmapButton.BackgroundColor = Color.FromArgb("#1A1114");
        HeatmapButton.TextColor = Colors.White;
    }

    private void OnHeatmapClicked(object sender, EventArgs e)
    {
        LevelsCollection.IsVisible = false;
        HeatmapContainer.IsVisible = true;

        HeatmapButton.BackgroundColor = Color.FromArgb("#C8A44D");
        HeatmapButton.TextColor = Color.FromArgb("#111827");

        CardsButton.BackgroundColor = Color.FromArgb("#1A1114");
        CardsButton.TextColor = Colors.White;
    }

    private async void OnHeatmapCenterClicked(object sender, EventArgs e)
    {
        if (_htmlLoaded)
        {
            await HeatmapWebView.EvaluateJavaScriptAsync(
                "zoomX=1; zoomY=1; panX=0; panY=0; draw();");
        }
    }

    private async void OnHeatmapClearClicked(object sender, EventArgs e)
    {
        _history.Clear();
        _storedCells.Clear();

        if (_htmlLoaded)
        {
            await HeatmapWebView.EvaluateJavaScriptAsync(
                "showMessage('Waiting for LOCMAP data...');");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private sealed class StoredLiquidityCell
    {
        public double Price { get; set; }
        public int Column { get; set; }
        public double Strength { get; set; }
        public bool Touched { get; set; }
        public int Life { get; set; }
        public DateTime LastSeen { get; set; }
    }

    private sealed class LiquidityHeatmapDto
    {
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double[] Heat { get; set; } = Array.Empty<double>();
        public double[] ColVol { get; set; } = Array.Empty<double>();
        public double Lo { get; set; }
        public double Hi { get; set; }
        public double Price { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
    }

    private sealed class LocmapMatrix
    {
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double[] Heat { get; set; } = Array.Empty<double>();
        public double[] Zones { get; set; } = Array.Empty<double>();
        public double[] ColVol { get; set; } = Array.Empty<double>();
        public double Lo { get; set; }
        public double Hi { get; set; }
        public double Price { get; set; }
        public string Symbol { get; set; } = "XAUUSD";
    }
}