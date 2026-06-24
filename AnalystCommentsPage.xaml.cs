using System.Collections.ObjectModel;
using System.Windows.Input;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public partial class AnalystCommentsPage : ContentPage
{
    private readonly AnalystService _analystService = new();
    private readonly ChatService _chatService = new();
    private readonly long _postId;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<AnalystCommentViewModel> Comments { get; } = new();

    public ICommand RefreshCommand { get; }

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

    private bool _isReplying;
    public bool IsReplying
    {
        get => _isReplying;
        set
        {
            _isReplying = value;
            OnPropertyChanged();
        }
    }

    private string _replyingToText = "";
    public string ReplyingToText
    {
        get => _replyingToText;
        set
        {
            _replyingToText = value;
            OnPropertyChanged();
        }
    }

    private string _replyPrefix = "";

    public AnalystCommentsPage(long postId)
    {
        InitializeComponent();
        BindingContext = this;
        _postId = postId;
        RefreshCommand = new Command(async () => await LoadCommentsAsync(true));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartAutoRefresh();

        if (Comments.Count == 0)
            await LoadCommentsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshCts?.Cancel();
        _refreshCts = null;
    }

    private async Task LoadCommentsAsync(bool isPullToRefresh = false)
    {
        try
        {
            IsRefreshing = isPullToRefresh;

            var page = await _analystService.GetPostCommentsAsync(_postId, 1, 100);
            var me = await _chatService.GetMeAsync(_analystService.GetOrCreateDeviceId());
            var currentUserId = me?.Id ?? 0;

            Comments.Clear();

            if (page is null)
                return;

            foreach (var comment in page.Items)
            {
                Comments.Add(new AnalystCommentViewModel
                {
                    Id = comment.Id,
                    PostId = comment.PostId,
                    UserId = comment.UserId,
                    UserName = comment.UserName,
                    AvatarUrl = _analystService.FixAvatarUrl(comment.AvatarUrl),
                    BadgeType = comment.BadgeType,
                    BadgeIcon = _analystService.GetBadgeIcon(comment.BadgeType),
                    HasBadge = _analystService.HasBadge(comment.BadgeType),
                    BadgeBorderColor = _analystService.GetBadgeBorderColor(comment.BadgeType),
                    BadgeIconBackgroundColor = _analystService.GetBadgeBackgroundColor(comment.BadgeType),
                    BadgeIconTextColor = _analystService.GetBadgeIconTextColor(comment.BadgeType),
                    Text = comment.Text,
                    CreatedAtUtc = comment.CreatedAtUtc,
                    TimeText = _analystService.FormatDateTime(comment.CreatedAtUtc),
                    CanDelete = currentUserId > 0 && currentUserId == comment.UserId,
                    CanReply = true
                });
            }
        }
        catch
        {
            await DisplayAlert("تنبيه", "تعذر تحميل التعليقات.", "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async void OnSendCommentClicked(object sender, EventArgs e)
    {
        var text = CommentEditor.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(text))
        {
            await DisplayAlert("تنبيه", "اكتب تعليق أولاً.", "OK");
            return;
        }

        var finalText = text;
        if (IsReplying && !string.IsNullOrWhiteSpace(_replyPrefix) && !text.StartsWith(_replyPrefix, StringComparison.Ordinal))
            finalText = $"{_replyPrefix} {text}".Trim();

        var deviceId = _analystService.GetOrCreateDeviceId();
        var result = await _analystService.AddCommentAsync(_postId, deviceId, finalText);

        if (result is null)
        {
            await DisplayAlert("تنبيه", "تعذر إرسال التعليق.", "OK");
            return;
        }

        CommentEditor.Text = "";
        ClearReplyState();
        await LoadCommentsAsync();
    }

    private async void OnDeleteCommentClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not long commentId)
            return;

        var confirm = await DisplayAlert("تأكيد", "هل تريد حذف هذا التعليق؟", "نعم", "لا");
        if (!confirm)
            return;

        var deviceId = _analystService.GetOrCreateDeviceId();
        var ok = await _analystService.DeleteOwnCommentAsync(commentId, deviceId);
        if (!ok)
        {
            await DisplayAlert("تنبيه", "تعذر حذف التعليق.", "OK");
            return;
        }

        await LoadCommentsAsync();
    }

    private void OnReplyCommentClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.BindingContext is not AnalystCommentViewModel comment)
            return;

        _replyPrefix = $"@{comment.UserName}";
        IsReplying = true;
        ReplyingToText = $"الرد على {comment.UserName}";
        CommentEditor.Text = $"{_replyPrefix} ";
        CommentEditor.Focus();
    }

    private void OnCancelReplyClicked(object sender, EventArgs e)
    {
        ClearReplyState();
    }

    private void ClearReplyState()
    {
        IsReplying = false;
        ReplyingToText = "";
        _replyPrefix = "";
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
                await Task.Delay(TimeSpan.FromSeconds(15), token);

                if (token.IsCancellationRequested)
                    break;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await LoadCommentsAsync();
                });
            }
        }
        catch (TaskCanceledException)
        {
        }
    }
}