using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace PROXIMAMOP;

public class HeatmapCanvasView : GraphicsView
{
    private readonly HeatmapDrawable _drawable;

    private double _lastPanY;
    private double _lastScale = 1;

    public HeatmapCanvasView()
    {
        _drawable = new HeatmapDrawable();
        Drawable = _drawable;

        BackgroundColor = Color.FromArgb("#020617");

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);
    }

    public void AddSnapshot(IEnumerable<HeatmapCanvasLevel> levels)
    {
        _drawable.AddSnapshot(levels);
        Invalidate();
    }

    public void Clear()
    {
        _drawable.Clear();
        Invalidate();
    }

    public void ResetView()
    {
        _drawable.ResetView();
        Invalidate();
    }

    public void SetAutoCenter(bool enabled)
    {
        _drawable.AutoCenter = enabled;
        Invalidate();
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started)
        {
            _lastPanY = e.TotalY;
            return;
        }

        if (e.StatusType == GestureStatus.Running)
        {
            var delta = e.TotalY - _lastPanY;
            _lastPanY = e.TotalY;

            _drawable.AutoCenter = false;
            _drawable.MovePriceOffset(delta);
            Invalidate();
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _lastScale = 1;
            return;
        }

        if (e.Status == GestureStatus.Running)
        {
            var change = e.Scale / _lastScale;
            _lastScale = e.Scale;

            _drawable.Zoom(change);
            Invalidate();
        }
    }
}

public class HeatmapCanvasLevel
{
    public double Price { get; set; }
    public double LastPrice { get; set; }

    public double Heat { get; set; }
    public string HeatColor { get; set; } = "#002B45";

    public double Contracts { get; set; }
    public int TotalTicks { get; set; }

    public double Delta { get; set; }
    public string Zone { get; set; } = "";

    public bool Absorption { get; set; }
    public bool AggressiveBuy { get; set; }
    public bool AggressiveSell { get; set; }
    public bool TickBurst { get; set; }
    public bool LiquiditySweep { get; set; }
    public bool ReactionZone { get; set; }
}

public class HeatmapDrawable : IDrawable
{
    private readonly List<List<HeatmapCanvasLevel>> _history = new();

    private const int MaxHistory = 160;

    private float _rowHeight = 15f;
    private float _columnWidth = 8f;
    private int _priceOffset = 0;

    public bool AutoCenter { get; set; } = true;

    public void AddSnapshot(IEnumerable<HeatmapCanvasLevel> levels)
    {
        var snapshot = levels
            .Where(x => x.Price > 0)
            .OrderByDescending(x => x.Price)
            .ToList();

        if (snapshot.Count == 0)
            return;

        _history.Add(snapshot);

        while (_history.Count > MaxHistory)
            _history.RemoveAt(0);
    }

    public void Clear()
    {
        _history.Clear();
        _priceOffset = 0;
    }

    public void ResetView()
    {
        _priceOffset = 0;
        AutoCenter = true;
    }

    public void MovePriceOffset(double deltaY)
    {
        var rows = (int)(deltaY / _rowHeight);

        if (rows == 0)
            rows = deltaY > 0 ? 1 : -1;

        _priceOffset -= rows;

        if (_priceOffset < 0)
            _priceOffset = 0;
    }

    public void Zoom(double scale)
    {
        _rowHeight = (float)Math.Clamp(_rowHeight * scale, 9, 28);
        _columnWidth = (float)Math.Clamp(_columnWidth * scale, 4, 18);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Color.FromArgb("#020617");
        canvas.FillRectangle(dirtyRect);

        if (_history.Count == 0)
        {
            DrawEmpty(canvas, dirtyRect);
            return;
        }

        var latest = _history.Last();

        var allPrices = _history
            .SelectMany(x => x.Select(y => y.Price))
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        if (allPrices.Count == 0)
        {
            DrawEmpty(canvas, dirtyRect);
            return;
        }

        var lastPrice = latest.FirstOrDefault(x => x.LastPrice > 0)?.LastPrice ?? latest.First().Price;

        var top = 30f;
        var bottom = dirtyRect.Height - 24f;
        var left = 6f;
        var priceAxisWidth = 74f;
        var heatRight = dirtyRect.Width - priceAxisWidth;
        var heatWidth = heatRight - left;

        var visibleRows = Math.Max(1, (int)((bottom - top) / _rowHeight));

        if (AutoCenter)
        {
            var nearestIndex = FindNearestPriceIndex(allPrices, lastPrice);
            _priceOffset = Math.Max(0, nearestIndex - visibleRows / 2);
        }

        if (_priceOffset > Math.Max(0, allPrices.Count - visibleRows))
            _priceOffset = Math.Max(0, allPrices.Count - visibleRows);

        var visiblePrices = allPrices
            .Skip(_priceOffset)
            .Take(visibleRows)
            .ToList();

        DrawHeader(canvas, dirtyRect, lastPrice, latest.Count);

        DrawGrid(canvas, visiblePrices, left, heatRight, top, priceAxisWidth);

        DrawHeat(canvas, visiblePrices, left, top, heatWidth);

        DrawLastPrice(canvas, visiblePrices, lastPrice, left, heatRight, top, dirtyRect.Width);

        DrawPriceAxis(canvas, visiblePrices, heatRight, top);
    }

    private void DrawEmpty(ICanvas canvas, RectF rect)
    {
        canvas.FontColor = Colors.White;
        canvas.FontSize = 22;
        canvas.DrawString(
            "No heatmap data",
            rect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawHeader(ICanvas canvas, RectF rect, double lastPrice, int levels)
    {
        canvas.FontSize = 12;
        canvas.FontColor = Color.FromArgb("#94A3B8");
        canvas.DrawString("LOCMAP HEATMAP", 8, 6, HorizontalAlignment.Left);

        canvas.FontColor = Color.FromArgb("#22C55E");
        canvas.DrawString("LIVE", rect.Width - 54, 6, HorizontalAlignment.Left);

        canvas.FontColor = Color.FromArgb("#C8A44D");
        canvas.DrawString($"Last {lastPrice:0.00}", rect.Width / 2 - 45, 6, HorizontalAlignment.Left);

        canvas.FontColor = Color.FromArgb("#94A3B8");
        canvas.DrawString($"{levels} levels", 8, 20, HorizontalAlignment.Left);
    }

    private void DrawGrid(
        ICanvas canvas,
        List<double> visiblePrices,
        float left,
        float heatRight,
        float top,
        float priceAxisWidth)
    {
        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 1;

        for (int i = 0; i < visiblePrices.Count; i++)
        {
            var y = top + i * _rowHeight;
            canvas.DrawLine(left, y, heatRight, y);
        }

        canvas.StrokeColor = Color.FromArgb("#334155");
        canvas.StrokeSize = 1;

        for (float x = heatRight; x > left; x -= _columnWidth * 12)
        {
            canvas.DrawLine(x, top, x, top + visiblePrices.Count * _rowHeight);
        }
    }

    private void DrawHeat(
        ICanvas canvas,
        List<double> visiblePrices,
        float left,
        float top,
        float heatWidth)
    {
        var maxColumns = Math.Max(1, (int)(heatWidth / _columnWidth));

        var start = Math.Max(0, _history.Count - maxColumns);
        var visibleHistory = _history.Skip(start).ToList();

        for (int c = 0; c < visibleHistory.Count; c++)
        {
            var snapshot = visibleHistory[c];

            var x = left + c * _columnWidth;

            foreach (var level in snapshot)
            {
                var row = visiblePrices.IndexOf(level.Price);

                if (row < 0)
                    continue;

                var y = top + row * _rowHeight;

                var color = ParseColor(level.HeatColor);
                var alpha = Math.Clamp(level.Heat / 100.0, 0.15, 1.0);

                canvas.FillColor = color.WithAlpha((float)alpha);
                canvas.FillRectangle(x, y, _columnWidth + 1, _rowHeight);

                if (level.Absorption || level.LiquiditySweep || level.TickBurst)
                {
                    canvas.FillColor = Colors.White.WithAlpha(0.20f);
                    canvas.FillRectangle(x, y, _columnWidth + 1, _rowHeight);
                }
            }
        }
    }

    private void DrawPriceAxis(
        ICanvas canvas,
        List<double> visiblePrices,
        float heatRight,
        float top)
    {
        canvas.FontSize = 11;
        canvas.FontColor = Color.FromArgb("#CBD5E1");

        for (int i = 0; i < visiblePrices.Count; i++)
        {
            var y = top + i * _rowHeight;
            canvas.DrawString(
                visiblePrices[i].ToString("0.00"),
                heatRight + 6,
                y - 2,
                HorizontalAlignment.Left);
        }
    }

    private void DrawLastPrice(
        ICanvas canvas,
        List<double> visiblePrices,
        double lastPrice,
        float left,
        float heatRight,
        float top,
        float fullWidth)
    {
        if (visiblePrices.Count == 0)
            return;

        var nearestIndex = FindNearestPriceIndex(visiblePrices, lastPrice);

        if (nearestIndex < 0)
            return;

        var y = top + nearestIndex * _rowHeight + _rowHeight / 2;

        canvas.StrokeColor = Color.FromArgb("#FFFFFF").WithAlpha(0.85f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(left, y, heatRight, y);

        canvas.FillColor = Color.FromArgb("#EF4444");
        canvas.FillRoundedRectangle(heatRight + 4, y - 10, 66, 20, 8);

        canvas.FontColor = Colors.White;
        canvas.FontSize = 11;
        canvas.DrawString(lastPrice.ToString("0.00"), heatRight + 9, y - 8, HorizontalAlignment.Left);
    }

    private static int FindNearestPriceIndex(List<double> prices, double price)
    {
        if (prices.Count == 0)
            return -1;

        var bestIndex = 0;
        var bestDistance = Math.Abs(prices[0] - price);

        for (int i = 1; i < prices.Count; i++)
        {
            var d = Math.Abs(prices[i] - price);

            if (d < bestDistance)
            {
                bestDistance = d;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Color.FromArgb("#002B45");

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            return Color.FromArgb(hex);
        }
        catch
        {
            return Color.FromArgb("#002B45");
        }
    }
}