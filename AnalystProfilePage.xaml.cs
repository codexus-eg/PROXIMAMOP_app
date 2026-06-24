using System.Collections.ObjectModel;
using System.Windows.Input;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public partial class AnalystProfilePage : ContentPage
{
    private readonly AnalystService _analystService = new();
    private readonly ChatService _chatService = new();
    private readonly int _analystId;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<AnalystPostViewModel> Posts { get; } = new();

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

    private string _analystName = "";
    public string AnalystName
    {
        get => _analystName;
        set
        {
            _analystName = value;
            OnPropertyChanged();
        }
    }

    private string _analystAvatarUrl = "";
    public string AnalystAvatarUrl
    {
        get => _analystAvatarUrl;
        set
        {
            _analystAvatarUrl = value;
            OnPropertyChanged();
        }
    }

    private string _analystBio = "";
    public string AnalystBio
    {
        get => _analystBio;
        set
        {
            _analystBio = value;
            OnPropertyChanged();
        }
    }

    private string _analystTitleText = "";
    public string AnalystTitleText
    {
        get => _analystTitleText;
        set
        {
            _analystTitleText = value;
            OnPropertyChanged();
        }
    }

    private string _analystAboutText = "";
    public string AnalystAboutText
    {
        get => _analystAboutText;
        set
        {
            _analystAboutText = value;
            OnPropertyChanged();
        }
    }

    private string _analystMethodologyText = "";
    public string AnalystMethodologyText
    {
        get => _analystMethodologyText;
        set
        {
            _analystMethodologyText = value;
            OnPropertyChanged();
        }
    }

    private bool _hasMethodology;
    public bool HasMethodology
    {
        get => _hasMethodology;
        set
        {
            _hasMethodology = value;
            OnPropertyChanged();
        }
    }

    private string _starsText = "";
    public string StarsText
    {
        get => _starsText;
        set
        {
            _starsText = value;
            OnPropertyChanged();
        }
    }

    private string _postsCountText = "0";
    public string PostsCountText
    {
        get => _postsCountText;
        set
        {
            _postsCountText = value;
            OnPropertyChanged();
        }
    }

    private bool _canCreatePost;
    public bool CanCreatePost
    {
        get => _canCreatePost;
        set
        {
            _canCreatePost = value;
            OnPropertyChanged();
        }
    }

    private bool _hasBadge;
    public bool HasBadge
    {
        get => _hasBadge;
        set
        {
            _hasBadge = value;
            OnPropertyChanged();
        }
    }

    private string _badgeIcon = "";
    public string BadgeIcon
    {
        get => _badgeIcon;
        set
        {
            _badgeIcon = value;
            OnPropertyChanged();
        }
    }

    private Color _badgeBorderColor = Color.FromArgb("#2E2E2E");
    public Color BadgeBorderColor
    {
        get => _badgeBorderColor;
        set
        {
            _badgeBorderColor = value;
            OnPropertyChanged();
        }
    }

    private Color _badgeIconBackgroundColor = Color.FromArgb("#2E2E2E");
    public Color BadgeIconBackgroundColor
    {
        get => _badgeIconBackgroundColor;
        set
        {
            _badgeIconBackgroundColor = value;
            OnPropertyChanged();
        }
    }
    private Color _badgeIconTextColor = Colors.White;
    public Color BadgeIconTextColor
    {
        get => _badgeIconTextColor;
        set
        {
            _badgeIconTextColor = value;
            OnPropertyChanged();
        }
    }

    private string _followButtonText = "متابعة";
    public string FollowButtonText
    {
        get => _followButtonText;
        set
        {
            _followButtonText = value;
            OnPropertyChanged();
        }
    }

    private Color _followButtonBackgroundColor = Color.FromArgb("#8B5CF6");
    public Color FollowButtonBackgroundColor
    {
        get => _followButtonBackgroundColor;
        set
        {
            _followButtonBackgroundColor = value;
            OnPropertyChanged();
        }
    }

    private string _notificationsButtonText = "تشغيل الإشعارات";
    public string NotificationsButtonText
    {
        get => _notificationsButtonText;
        set
        {
            _notificationsButtonText = value;
            OnPropertyChanged();
        }
    }

    private Color _notificationsButtonBackgroundColor = Color.FromArgb("#25365C");
    public Color NotificationsButtonBackgroundColor
    {
        get => _notificationsButtonBackgroundColor;
        set
        {
            _notificationsButtonBackgroundColor = value;
            OnPropertyChanged();
        }
    }

    private string _followInfoText = "";
    public string FollowInfoText
    {
        get => _followInfoText;
        set
        {
            _followInfoText = value;
            OnPropertyChanged();
        }
    }

    public AnalystProfilePage(int analystId)
    {
        InitializeComponent();
        BindingContext = this;
        _analystId = analystId;
        RefreshCommand = new Command(async () => await LoadDataAsync(true));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartAutoRefresh();
        await LoadDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshCts?.Cancel();
        _refreshCts = null;
    }

    private async Task LoadDataAsync(bool isPullToRefresh = false)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = !isPullToRefresh;
            IsRefreshing = isPullToRefresh;

            var analyst = await _analystService.GetAnalystByIdAsync(_analystId);
            if (analyst is null)
            {
                await DisplayAlert("تنبيه", "تعذر تحميل بيانات المحلل.", "OK");
                await Navigation.PopAsync();
                return;
            }

            AnalystName = analyst.Name;
            AnalystAvatarUrl = _analystService.FixAvatarUrl(analyst.AvatarUrl);
            AnalystBio = string.IsNullOrWhiteSpace(analyst.AnalystBio) ? analyst.Bio : analyst.AnalystBio;
            AnalystTitleText = string.IsNullOrWhiteSpace(analyst.Bio) ? "محلل مالي" : analyst.Bio;
            AnalystAboutText = string.IsNullOrWhiteSpace(analyst.Bio)
                ? "لا توجد سيرة ذاتية مضافة حالياً."
                : analyst.Bio;

            HasMethodology = !string.IsNullOrWhiteSpace(analyst.AnalystBio);
            AnalystMethodologyText = analyst.AnalystBio;

            StarsText = _analystService.GetStarsText(analyst.AnalystStars);

            HasBadge = _analystService.HasBadge(analyst.BadgeType);
            BadgeIcon = _analystService.GetBadgeIcon(analyst.BadgeType);
            BadgeBorderColor = _analystService.GetBadgeBorderColor(analyst.BadgeType);
            BadgeIconBackgroundColor = _analystService.GetBadgeBackgroundColor(analyst.BadgeType);
            BadgeIconTextColor = _analystService.GetBadgeIconTextColor(analyst.BadgeType);

            ApplyLocalFollowState();

            var deviceId = _analystService.GetOrCreateDeviceId();
            var me = await _chatService.GetMeAsync(deviceId);

            CanCreatePost =
                me is not null &&
                me.Id == analyst.Id &&
                me.IsAnalyst &&
                me.CanPostAnalystContent;
            var postsPage = await _analystService.GetAnalystPostsAsync(_analystId, 1, 50);

            Posts.Clear();

            if (postsPage is not null)
            {
                var currentUserId = me?.Id ?? 0;

                foreach (var post in postsPage.Items)
                {
                    Posts.Add(new AnalystPostViewModel
                    {
                        Id = post.Id,
                        UserId = post.UserId,
                        UserName = post.UserName,
                        AvatarUrl = _analystService.FixAvatarUrl(post.AvatarUrl),
                        BadgeType = post.BadgeType,
                        BadgeIcon = _analystService.GetBadgeIcon(post.BadgeType),
                        HasBadge = _analystService.HasBadge(post.BadgeType),
                        BadgeBorderColor = _analystService.GetBadgeBorderColor(post.BadgeType),
                        BadgeIconBackgroundColor = _analystService.GetBadgeBackgroundColor(post.BadgeType),
                        BadgeIconTextColor = _analystService.GetBadgeIconTextColor(post.BadgeType),
                        AnalystStars = post.AnalystStars,
                        StarsText = _analystService.GetStarsText(post.AnalystStars),
                        Text = post.Text,
                        HasText = !string.IsNullOrWhiteSpace(post.Text),
                        ImageUrl = string.IsNullOrWhiteSpace(post.ImageUrl) ? "" : _analystService.FixFileUrl(post.ImageUrl),
                        HasImage = !string.IsNullOrWhiteSpace(post.ImageUrl),
                        CreatedAtUtc = post.CreatedAtUtc,
                        TimeText = _analystService.FormatDateTime(post.CreatedAtUtc),
                        CommentsCount = post.CommentsCount,
                        CommentsText = post.CommentsCount == 0 ? "لا توجد تعليقات" : $"{post.CommentsCount} تعليق",
                        CanDelete = currentUserId > 0 && currentUserId == post.UserId
                    });
                }

                PostsCountText = $"{postsPage.TotalCount} منشور";
            }
            else
            {
                PostsCountText = "0 منشور";
            }
        }
        catch
        {
            await DisplayAlert("تنبيه", " خطأ أثناء تحميل الصفحة.", "OK");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private void ApplyLocalFollowState()
    {
        var isFollowing = _analystService.IsFollowingAnalyst(_analystId);
        var notificationsEnabled = _analystService.IsAnalystNotificationsEnabled(_analystId);

        FollowButtonText = isFollowing ? "تمت المتابعة" : "متابعة";
        FollowButtonBackgroundColor = isFollowing
            ? Color.FromArgb("#1E7A52")
            : Color.FromArgb("#8B5CF6");

        NotificationsButtonText = notificationsEnabled ? "إشعارات مفعلة" : "تشغيل الإشعارات";
        NotificationsButtonBackgroundColor = notificationsEnabled
            ? Color.FromArgb("#0E7490")
            : Color.FromArgb("#25365C");

        FollowInfoText = notificationsEnabled
            ? "تم تشغيل الإشعارات."
            : isFollowing
                ? "تمت المتابعة."
                : "يمكنك المتابعة وتشغيل الإشعارات.";
    }

    private async void OnOpenCommentsClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not long postId)
            return;

        await Navigation.PushAsync(new AnalystCommentsPage(postId));
    }

    private async void OnCreatePostClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CreateAnalystPostPage(_analystId));
    }

    private async void OnDeletePostClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not long postId)
            return;

        var confirm = await DisplayAlert("تأكيد", "هل تريد حذف هذا المنشور؟", "نعم", "لا");
        if (!confirm)
            return;
        var deviceId = _analystService.GetOrCreateDeviceId();
        var ok = await _analystService.DeleteOwnPostAsync(postId, deviceId);

        if (!ok)
        {
            await DisplayAlert("تنبيه", "تعذر حذف المنشور.", "OK");
            return;
        }

        await LoadDataAsync();
    }

    private async void OnToggleFollowClicked(object sender, EventArgs e)
    {
        var current = _analystService.IsFollowingAnalyst(_analystId);
        var newValue = !current;

        _analystService.SetFollowingAnalyst(_analystId, newValue);

        if (!newValue)
            _analystService.SetAnalystNotificationsEnabled(_analystId, false);

        ApplyLocalFollowState();

        await DisplayAlert("تم", newValue ? "تمت المتابعة." : "تم إلغاء المتابعة.", "OK");
    }

    private async void OnToggleNotificationsClicked(object sender, EventArgs e)
    {
        var current = _analystService.IsAnalystNotificationsEnabled(_analystId);
        var newValue = !current;

        if (newValue && !_analystService.IsFollowingAnalyst(_analystId))
            _analystService.SetFollowingAnalyst(_analystId, true);

        _analystService.SetAnalystNotificationsEnabled(_analystId, newValue);
        ApplyLocalFollowState();

        await DisplayAlert("تم", newValue ? "تم تشغيل الإشعارات." : "تم إيقاف الإشعارات.", "OK");
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
                await Task.Delay(TimeSpan.FromSeconds(20), token);

                if (token.IsCancellationRequested)
                    break;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await LoadDataAsync();
                });
            }
        }
        catch (TaskCanceledException)
        {
        }
    }
}