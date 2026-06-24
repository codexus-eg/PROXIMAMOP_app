using System.Collections.ObjectModel;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class FeedPage : ContentPage
{
    private readonly FeedService _feedService = new();
    private readonly ObservableCollection<FeedItem> _items = new();

    private PeriodicTimer? _pollTimer;
    private bool _isPageActive;
    private bool _isLoading;

    private int _lastKnownId = 0;
    private int _lastKnownCount = 0;

    public FeedPage()
    {
        InitializeComponent();

        FeedCollectionView.ItemsSource = _items;
        UpdateFeedCount();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isPageActive = true;

        await LoadFeedAsync(showOverlay: true, updateState: true);

        _pollTimer?.Dispose();
        _pollTimer = null;

        StartPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _isPageActive = false;

        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private void SetBusy(bool isBusy, bool showOverlay)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isBusy;
            LoadingIndicator.IsRunning = isBusy;
            BusyOverlay.IsVisible = isBusy && showOverlay;
        });
    }

    private void UpdateFeedCount()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FeedCountLabel.Text = _items.Count.ToString();
        });
    }

    private async Task LoadFeedAsync(bool showOverlay, bool updateState)
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            SetBusy(true, showOverlay);

            var items = await _feedService.GetFeedAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _items.Clear();

                foreach (var item in items)
                    _items.Add(item);

                UpdateFeedCount();
            });

            if (updateState)
                UpdateKnownState(items);
        }
        finally
        {
            _isLoading = false;
            SetBusy(false, false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FeedRefreshView.IsRefreshing = false;
            });
        }
    }

    private void StartPolling()
    {
        if (_pollTimer != null)
            return;

        _pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        _ = Task.Run(async () =>
        {
            while (_isPageActive &&
                   _pollTimer != null &&
                   await _pollTimer.WaitForNextTickAsync())
            {
                if (_isLoading)
                    continue;

                try
                {
                    var fullFeed = await _feedService.GetFeedAsync();

                    var newestId = fullFeed.Count > 0
                        ? fullFeed.Max(x => x.Id)
                        : 0;

                    var feedCount = fullFeed.Count;

                    var hasChanged =
                        newestId != _lastKnownId ||
                        feedCount != _lastKnownCount;

                    if (!hasChanged)
                        continue;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _items.Clear();

                        foreach (var item in fullFeed)
                            _items.Add(item);

                        UpdateFeedCount();
                    });

                    UpdateKnownState(fullFeed);
                }
                catch
                {
                }
            }
        });
    }

    private void UpdateKnownState(List<FeedItem> items)
    {
        _lastKnownId = items.Count > 0
            ? items.Max(x => x.Id)
            : 0;

        _lastKnownCount = items.Count;
    }

    private async void OnRefreshRequested(object sender, EventArgs e)
    {
        await LoadFeedAsync(showOverlay: false, updateState: true);
    }
}