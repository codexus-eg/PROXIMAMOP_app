using System.Collections.ObjectModel;
using System.Windows.Input;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public partial class AnalystsPage : ContentPage
{
    private readonly AnalystService _analystService = new();
    private readonly List<AnalystListItemViewModel> _allAnalysts = new();
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<AnalystListItemViewModel> Analysts { get; } = new();

    public ICommand RefreshCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public AnalystsPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadAnalystsAsync(true));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        StartAutoRefresh();

        if (Analysts.Count == 0)
            await LoadAnalystsAsync();
        else
            ApplyFilter();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshCts?.Cancel();
        _refreshCts = null;
    }

    private async Task LoadAnalystsAsync(bool isPullToRefresh = false)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = !isPullToRefresh;
            IsRefreshing = isPullToRefresh;

            var items = await _analystService.GetAnalystsAsync();

            _allAnalysts.Clear();

            foreach (var item in items)
            {
                var isFollowing = _analystService.IsFollowingAnalyst(item.Id);
                var notificationsEnabled = _analystService.IsAnalystNotificationsEnabled(item.Id);

                _allAnalysts.Add(new AnalystListItemViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    AvatarUrl = _analystService.FixAvatarUrl(item.AvatarUrl),
                    Bio = item.Bio,
                    AnalystBio = string.IsNullOrWhiteSpace(item.AnalystBio) ? item.Bio : item.AnalystBio,
                    Subtitle = string.IsNullOrWhiteSpace(item.Bio) ? "محلل مالي" : item.Bio,
                    AnalystStars = item.AnalystStars,
                    StarsText = _analystService.GetStarsText(item.AnalystStars),
                    BadgeType = item.BadgeType,
                    BadgeIcon = _analystService.GetBadgeIcon(item.BadgeType),
                    HasBadge = _analystService.HasBadge(item.BadgeType),
                    BadgeBorderColor = _analystService.GetBadgeBorderColor(item.BadgeType),
                    BadgeIconBackgroundColor = _analystService.GetBadgeBackgroundColor(item.BadgeType),
                    BadgeIconTextColor = _analystService.GetBadgeIconTextColor(item.BadgeType),
                    IsFollowing = isFollowing,
                    FollowStateText = isFollowing ? "متابَع" : "غير متابَع",
                    FollowStateBackgroundColor = isFollowing ? Color.FromArgb("#1E7A52") : Color.FromArgb("#24304A"),
                    FollowStateTextColor = Colors.White,
                    NotificationsEnabled = notificationsEnabled,
                    NotificationStateText = notificationsEnabled ? "إشعارات مفعلة" : "إشعارات متوقفة",
                    NotificationStateBackgroundColor = notificationsEnabled ? Color.FromArgb("#18608A") : Color.FromArgb("#2B2740"),
                    NotificationStateTextColor = Colors.White
                });
            }

            ApplyFilter();
        }
        catch
        {
            await DisplayAlert("تنبيه", "صار خطأ أثناء تحميل قائمة المحللين.", "OK");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }
    private void ApplyFilter()
    {
        var search = SearchBarControl?.Text?.Trim() ?? "";

        IEnumerable<AnalystListItemViewModel> query = _allAnalysts;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.AnalystBio.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Bio.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Analysts.Clear();

        foreach (var item in query)
            Analysts.Add(item);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void OnOpenAnalystClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not int analystId)
            return;

        await Navigation.PushAsync(new AnalystProfilePage(analystId));
    }

    private void StartAutoRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        _ = RunAutoRefreshAsync(_refreshCts.Token);
    }

    private async Task RunAutoRefreshAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);

                if (token.IsCancellationRequested)
                    break;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await LoadAnalystsAsync();
                });
            }
        }
        catch (TaskCanceledException)
        {
        }
    }
}
