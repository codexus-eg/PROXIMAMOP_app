using System.Collections.ObjectModel;
using Plugin.Maui.Audio;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class ChatPage : ContentPage
{
    private const int CacheLimit = 100;
    private const int OlderPageSize = 40;

    private readonly ChatService _chatService = new();
    private readonly ChatMessageCacheService _cacheService = new();
    private readonly ObservableCollection<ChatMessageViewModel> _messages = new();

    private string _deviceId = "";
    private string _savedName = "";
    private ChatUserDto? _me;
    private bool _isInitialized;
    private bool _isRealtimeStarted;
    private bool _isLoadingMessages;
    private bool _isLoadingOlder;
    private bool _hasMoreOlderMessages = true;

    private readonly IAudioRecorder _audioRecorder;
    private IAudioPlayer? _audioPlayer;
    private Stream? _audioPlaybackStream;

    private bool _isRecordingVoice;
    private string? _pendingVoiceFilePath;
    private int? _pendingVoiceDurationSeconds;
    private DateTime _recordingStartedAtUtc;

    private long? _oldestLoadedMessageId;

    public ChatPage()
    {
        InitializeComponent();

        _audioRecorder = AudioManager.Current.CreateRecorder();
        MessagesCollection.ItemsSource = _messages;
        MessagesCollection.Scrolled += OnMessagesScrolled;

        DisableChat();
        NoticeBorder.IsVisible = false;
        JoinRequestBorder.IsVisible = false;
        LiveShortcutBorder.IsVisible = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await InitializeChatAsync();
                return;
            }

            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            ShowError($"خطأ: {ex.Message}");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        try
        {
            await _chatService.StopSignalRAsync();
            _isRealtimeStarted = false;
        }
        catch
        {
        }

        StopVoicePlayback();
    }

    private async Task InitializeChatAsync()
    {
        try
        {
            SetLoading(true);
            StatusLabel.Text = "جارِ التحميل...";

            _deviceId = _chatService.GetOrCreateDeviceId();
            _savedName = _chatService.GetSavedName().Trim();

            if (string.IsNullOrWhiteSpace(_savedName))
            {
                ShowJoinRequestState();
                return;
            }

            await EnsureUserExistsAsync();

            if (_me is null)
            {
                ShowJoinRequestState();
                return;
            }

            await ApplyUserStateAsync();
        }
        catch (Exception ex)
        {
            ShowError($"خطأ: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task RefreshStateAsync()
    {
        try
        {
            SetLoading(true);
            StatusLabel.Text = "جارِ التحديث...";

            if (string.IsNullOrWhiteSpace(_deviceId))
                _deviceId = _chatService.GetOrCreateDeviceId();

            if (string.IsNullOrWhiteSpace(_savedName))
                _savedName = _chatService.GetSavedName().Trim();

            _me = await _chatService.GetMeAsync(_deviceId);

            if (_me is null && !string.IsNullOrWhiteSpace(_savedName))
                _me = await _chatService.RegisterOrUpdateAsync(_deviceId, _savedName);

            if (_me is null)
            {
                ShowJoinRequestState();
                return;
            }

            await ApplyUserStateAsync();
        }
        catch (Exception ex)
        {
            ShowError($"خطأ: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task EnsureUserExistsAsync()
    {
        _me = await _chatService.GetMeAsync(_deviceId);

        if (_me is not null)
            return;

        if (string.IsNullOrWhiteSpace(_savedName))
            return;

        _me = await _chatService.RegisterOrUpdateAsync(_deviceId, _savedName);
    }

    private async Task ApplyUserStateAsync()
    {
        if (_me is null)
        {
            ShowJoinRequestState();
            return;
        }

        switch (_me.Status)
        {
            case "Pending":
                await StopRealtimeIfNeededAsync();
                _messages.Clear();

                JoinRequestBorder.IsVisible = false;
                NoticeBorder.IsVisible = true;
                NoticeLabel.Text = "تم إرسال طلب الانضمام. انتظر موافقة الإدارة ثم اضغط زر التحديث.";
                StatusLabel.Text = "طلبك قيد المراجعة";
                DisableChat();
                HideLiveShortcut();
                break;

            case "Rejected":
                await StopRealtimeIfNeededAsync();
                _messages.Clear();

                JoinRequestBorder.IsVisible = false;
                NoticeBorder.IsVisible = true;
                NoticeLabel.Text = "تم رفض طلب الانضمام من الإدارة.";
                StatusLabel.Text = "تم رفض الطلب";
                DisableChat();
                HideLiveShortcut();
                break;

            case "Banned":
                await StopRealtimeIfNeededAsync();
                _messages.Clear();

                JoinRequestBorder.IsVisible = false;
                NoticeBorder.IsVisible = true;
                NoticeLabel.Text = string.IsNullOrWhiteSpace(_me.BanReason)
                    ? "تم حظرك من الدردشة."
                    : $"تم حظرك من الدردشة. السبب: {_me.BanReason}";
                StatusLabel.Text = "أنت محظور";
                DisableChat();
                HideLiveShortcut();
                break;

            case "Approved":
                JoinRequestBorder.IsVisible = false;
                NoticeBorder.IsVisible = false;
                StatusLabel.Text = $"مفعل - {_me.Role}";
                EnableChat();
                ShowLiveShortcut();

                await LoadMessagesAsync(fullRefresh: true, scrollToBottom: true);
                await StartRealtimeIfNeededAsync();
                break;

            default:
                await StopRealtimeIfNeededAsync();
                _messages.Clear();

                NoticeBorder.IsVisible = true;
                NoticeLabel.Text = "تعذر تحديد حالة المستخدم.";
                StatusLabel.Text = "حالة غير معروفة";
                DisableChat();
                HideLiveShortcut();
                break;
        }
    }

    private void ShowJoinRequestState()
    {
        _messages.Clear();
        DisableChat();

        NoticeBorder.IsVisible = false;
        JoinRequestBorder.IsVisible = true;
        StatusLabel.Text = "بانتظار إرسال الطلب";
        JoinNameEntry.Text = _savedName;
        HideLiveShortcut();
    }

    private async Task LoadMessagesAsync(bool fullRefresh, bool scrollToBottom = false)
    {
        if (_isLoadingMessages)
            return;

        try
        {
            _isLoadingMessages = true;

            if (fullRefresh)
            {
                var cached = await _cacheService.GetCachedMessagesAsync();
                if (cached.Count > 0 && _messages.Count == 0)
                {
                    ReplaceVisibleMessages(cached);
                    if (scrollToBottom)
                        await ScrollToBottomAsync();
                }

                var latest = await _chatService.GetMessagesAsync(CacheLimit);
                latest = latest
                    .OrderBy(x => x.CreatedAtUtc)
                    .ThenBy(x => x.Id)
                    .ToList();

                if (latest.Count > 0)
                {
                    await _cacheService.SaveLatestMessagesAsync(latest);
                    ReplaceVisibleMessages(latest);

                    if (latest.Count < CacheLimit)
                        _hasMoreOlderMessages = false;

                    if (scrollToBottom)
                        await ScrollToBottomAsync();
                }

                return;
            }

            var incremental = await _chatService.GetMessagesAsync(20);
            incremental = incremental
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

            if (incremental.Count == 0)
                return;

            MergeIncomingMessages(incremental);
            await _cacheService.AppendOrUpdateMessagesAsync(incremental);

            if (scrollToBottom)
                await ScrollToBottomAsync();
        }
        finally
        {
            _isLoadingMessages = false;
        }
    }

    private async Task LoadOlderMessagesAsync()
    {
        if (_isLoadingOlder || _isLoadingMessages || !_hasMoreOlderMessages)
            return;

        if (!_oldestLoadedMessageId.HasValue || _oldestLoadedMessageId.Value <= 0)
            return;

        try
        {
            _isLoadingOlder = true;

            var firstVisibleId = _messages.FirstOrDefault()?.Id;

            var older = await _chatService.GetMessagesAsync(
                take: OlderPageSize,
                beforeMessageId: _oldestLoadedMessageId.Value);

            older = older
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

            if (older.Count == 0)
            {
                _hasMoreOlderMessages = false;
                return;
            }

            var existingIds = _messages.Select(x => x.Id).ToHashSet();
            var itemsToInsert = older
                .Where(x => !existingIds.Contains(x.Id))
                .ToList();

            if (itemsToInsert.Count == 0)
            {
                if (older.Count < OlderPageSize)
                    _hasMoreOlderMessages = false;

                return;
            }

            for (var i = 0; i < itemsToInsert.Count; i++)
                _messages.Insert(i, ToViewModel(itemsToInsert[i]));

            UpdateOldestLoadedMessageId();

            if (older.Count < OlderPageSize)
                _hasMoreOlderMessages = false;

            await Task.Delay(50);

            if (firstVisibleId.HasValue)
            {
                var keepItem = _messages.FirstOrDefault(x => x.Id == firstVisibleId.Value);
                if (keepItem is not null)
                    MessagesCollection.ScrollTo(keepItem, position: ScrollToPosition.Start, animate: false);
            }
        }
        finally
        {
            _isLoadingOlder = false;
        }
    }

    private void ReplaceVisibleMessages(IEnumerable<ChatMessageDto> items)
    {
        _messages.Clear();

        foreach (var item in items.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id))
            _messages.Add(ToViewModel(item));

        UpdateOldestLoadedMessageId();
        _hasMoreOlderMessages = _messages.Count >= CacheLimit;
    }

    private void MergeIncomingMessages(IEnumerable<ChatMessageDto> incoming)
    {
        var changed = false;
        var map = _messages.ToDictionary(x => x.Id, x => x);

        foreach (var item in incoming.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id))
        {
            if (map.TryGetValue(item.Id, out var existing))
            {
                var updated = ToViewModel(item);

                if (!AreEquivalent(existing, updated))
                {
                    var index = _messages.IndexOf(existing);
                    if (index >= 0)
                    {
                        _messages[index] = updated;
                        changed = true;
                    }
                }
            }
            else
            {
                _messages.Add(ToViewModel(item));
                changed = true;
            }
        }

        if (!changed)
        {
            UpdateOldestLoadedMessageId();
            return;
        }

        var ordered = _messages
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToList();

        _messages.Clear();

        foreach (var item in ordered)
            _messages.Add(item);

        UpdateOldestLoadedMessageId();
    }

    private static bool AreEquivalent(ChatMessageViewModel a, ChatMessageViewModel b)
    {
        return a.Id == b.Id &&
               a.Text == b.Text &&
               a.Type == b.Type &&
               a.ImageUrl == b.ImageUrl &&
               a.VoiceUrl == b.VoiceUrl &&
               a.VoiceDurationText == b.VoiceDurationText &&
               a.TimeText == b.TimeText &&
               a.BadgeType == b.BadgeType &&
               a.AvatarUrl == b.AvatarUrl &&
               a.UserName == b.UserName;
    }

    private void UpdateOldestLoadedMessageId()
    {
        _oldestLoadedMessageId = _messages.Count == 0
            ? null
            : _messages.Min(x => x.Id);
    }

    private async Task StartRealtimeIfNeededAsync()
    {
        if (_isRealtimeStarted)
            return;

        await _chatService.StartSignalRAsync(
            _deviceId,
            onReceive: message =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (_messages.Any(x => x.Id == message.Id))
                        return;

                    var dtoList = new List<ChatMessageDto> { message };
                    MergeIncomingMessages(dtoList);
                    await _cacheService.AppendOrUpdateMessagesAsync(dtoList);
                    await ScrollToBottomAsync();
                });
            },
            onDelete: messageId =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var existing = _messages.FirstOrDefault(x => x.Id == messageId);
                    if (existing is not null)
                        _messages.Remove(existing);

                    var cached = await _cacheService.GetCachedMessagesAsync();
                    var updated = cached.Where(x => x.Id != messageId).ToList();
                    await _cacheService.SaveLatestMessagesAsync(updated);

                    UpdateOldestLoadedMessageId();
                });
            });

        _isRealtimeStarted = true;
    }

    private async Task StopRealtimeIfNeededAsync()
    {
        if (!_isRealtimeStarted)
            return;

        await _chatService.StopSignalRAsync();
        _isRealtimeStarted = false;
    }

    private void OnMessagesScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.FirstVisibleItemIndex <= 1)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadOlderMessagesAsync();
            });
        }
    }

    private ChatMessageViewModel ToViewModel(ChatMessageDto dto)
    {
        var isMine = _me is not null && dto.UserId == _me.Id;
        var type = dto.Type?.Trim() ?? "Text";

        var fixedFileUrl = string.IsNullOrWhiteSpace(dto.FileUrl)
            ? ""
            : _chatService.FixFileUrl(dto.FileUrl);

        var badgeType = string.IsNullOrWhiteSpace(dto.BadgeType) ? "None" : dto.BadgeType;

        var isSystem =
            type.Equals("System", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Broadcast", StringComparison.OrdinalIgnoreCase);

        var isVoice =
            type.Equals("Voice", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(fixedFileUrl);

        return new ChatMessageViewModel
        {
            Id = dto.Id,
            UserId = dto.UserId,
            UserName = dto.UserName,
            AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl)
                ? "dotnet_bot.png"
                : _chatService.FixAvatarUrl(dto.AvatarUrl),
            Text = dto.Text,
            TimeText = dto.CreatedAtUtc.ToLocalTime().ToString("hh:mm tt"),
            CreatedAtUtc = dto.CreatedAtUtc,
            Type = type,
            BadgeType = badgeType,
            IsMine = isMine,
            IsImage = type.Equals("Image", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(fixedFileUrl),
            ImageUrl = fixedFileUrl,
            IsVoice = isVoice,
            VoiceUrl = isVoice ? fixedFileUrl : "",
            VoiceDurationText = FormatDuration(dto.DurationSeconds),
            HasText = !string.IsNullOrWhiteSpace(dto.Text),
            IsSystem = isSystem,
            BadgeIcon = _chatService.GetBadgeIcon(badgeType),
            HasBadgeIcon = _chatService.HasBadge(badgeType),
            BadgeBorderColor = _chatService.GetBadgeBorderColor(badgeType),
            BadgeIconBackgroundColor = _chatService.GetBadgeBackgroundColor(badgeType),
            BadgeIconTextColor = _chatService.GetBadgeIconTextColor(badgeType),
            BubbleColor = isSystem
                ? Color.FromArgb("#E63A2F12")
                : isMine
                    ? Color.FromArgb("#E62B175A")
                    : Color.FromArgb("#E6111111"),
            TextColor = Colors.White,
            MessageRowHorizontalOptions = isMine ? LayoutOptions.Start : LayoutOptions.End,
            HeaderHorizontalOptions = isMine ? LayoutOptions.Start : LayoutOptions.End,
            MessageTextAlignment = isMine ? TextAlignment.Start : TextAlignment.End,
            ShowAvatar = true,
            ShowBadgeOnAvatar = !isSystem && _chatService.HasBadge(badgeType),
            ShowBadgeNearName = false
        };
    }

    private void DisableChat()
    {
        MessageEntry.IsEnabled = false;
        SendButton.IsEnabled = false;
        ImageButton.IsEnabled = false;
        VoiceButton.IsEnabled = false;
        RefreshButton.IsEnabled = true;
    }

    private void EnableChat()
    {
        MessageEntry.IsEnabled = true;
        SendButton.IsEnabled = true;
        ImageButton.IsEnabled = true;
        VoiceButton.IsEnabled = true;
        RefreshButton.IsEnabled = true;
    }

    private void SetLoading(bool isLoading)
    {
        TopLoading.IsVisible = isLoading;
        TopLoading.IsRunning = isLoading;
    }

    private void ShowError(string message)
    {
        JoinRequestBorder.IsVisible = false;
        NoticeBorder.IsVisible = true;
        NoticeLabel.Text = message;
        StatusLabel.Text = "فشل الاتصال";
        DisableChat();
        HideLiveShortcut();
    }

    private async Task ScrollToBottomAsync()
    {
        if (_messages.Count == 0)
            return;

        await Task.Delay(100);
        MessagesCollection.ScrollTo(_messages.Count - 1, position: ScrollToPosition.End, animate: false);
    }

    private async void OnRequestJoinClicked(object sender, EventArgs e)
    {
        try
        {
            var enteredName = JoinNameEntry.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(enteredName))
            {
                await DisplayAlert("تنبيه", "اكتب اسم المستخدم أولاً.", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(_deviceId))
                _deviceId = _chatService.GetOrCreateDeviceId();

            SetLoading(true);
            RequestJoinButton.IsEnabled = false;

            _savedName = enteredName;
            _chatService.SaveName(_savedName);

            _me = await _chatService.RegisterOrUpdateAsync(_deviceId, _savedName);

            if (_me is null)
            {
                ShowError("فشل إرسال طلب الانضمام إلى السيرفر.");
                return;
            }

            await ApplyUserStateAsync();
        }
        finally
        {
            SetLoading(false);
            RequestJoinButton.IsEnabled = true;
        }
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_me is null || _me.Status != "Approved")
                return;

            if (!string.IsNullOrWhiteSpace(_pendingVoiceFilePath))
            {
                await SendPendingVoiceAsync();
                return;
            }

            var text = MessageEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (text.Length > 1000)
            {
                await DisplayAlert("تنبيه", "الحد الأقصى للرسالة هو 1000 حرف.", "حسناً");
                return;
            }

            SendButton.IsEnabled = false;
            var ok = await _chatService.SendMessageAsync(_deviceId, text);

            if (ok)
            {
                MessageEntry.Text = "";
                await Task.Delay(200);
                await LoadMessagesAsync(fullRefresh: false, scrollToBottom: true);
            }
            else
            {
                await DisplayAlert("تنبيه", "فشل إرسال الرسالة.", "حسناً");
            }
        }
        finally
        {
            SendButton.IsEnabled = true;
            UpdateSendButtonText();
        }
    }

    private async Task SendPendingVoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(_pendingVoiceFilePath))
            return;

        SendButton.IsEnabled = false;

        var ok = await _chatService.SendVoiceAsync(_deviceId, _pendingVoiceFilePath, _pendingVoiceDurationSeconds);

        if (ok)
        {
            DeletePendingVoiceFile();
            StatusLabel.Text = $"مفعل - {_me?.Role}";
            await Task.Delay(200);
            await LoadMessagesAsync(fullRefresh: false, scrollToBottom: true);
        }
        else
        {
            await DisplayAlert("تنبيه", "فشل إرسال الرسالة الصوتية.", "حسناً");
        }
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            if (_me is null || _me.Status != "Approved")
                return;

            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null)
                return;

            ImageButton.IsEnabled = false;

            var ok = await _chatService.SendImageAsync(_deviceId, photo);
            if (ok)
            {
                await Task.Delay(200);
                await LoadMessagesAsync(fullRefresh: false, scrollToBottom: true);
            }
            else
            {
                await DisplayAlert("تنبيه", "فشل إرسال الصورة.", "حسناً");
            }
        }
        finally
        {
            ImageButton.IsEnabled = true;
        }
    }

    private async void OnVoiceClicked(object sender, EventArgs e)
    {
        if (_isRecordingVoice)
            await StopVoiceRecordingAsync();
        else
            await StartVoiceRecordingAsync();
    }

    private async Task StartVoiceRecordingAsync()
    {
        try
        {
            if (_me is null || _me.Status != "Approved")
                return;

            var permission = await Permissions.RequestAsync<Permissions.Microphone>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlert("تنبيه", "لم يتم منح صلاحية الميكروفون.", "حسناً");
                return;
            }

            DeletePendingVoiceFile();

            var fileName = $"voice_{DateTime.UtcNow:yyyyMMdd_HHmmss}.wav";
            _pendingVoiceFilePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            _recordingStartedAtUtc = DateTime.UtcNow;
            await _audioRecorder.StartAsync(_pendingVoiceFilePath);

            _isRecordingVoice = true;
            VoiceButton.BackgroundColor = Color.FromArgb("#B42318");
            VoiceButton.Text = "⏹️";
            StatusLabel.Text = "جارٍ تسجيل الرسالة الصوتية...";
        }
        catch (Exception ex)
        {
            _isRecordingVoice = false;
            VoiceButton.BackgroundColor = Color.FromArgb("#2A2A2A");
            VoiceButton.Text = "🎤";
            await DisplayAlert("تنبيه", $"تعذر بدء التسجيل: {ex.Message}", "حسناً");
        }
    }

    private async Task StopVoiceRecordingAsync()
    {
        try
        {
            if (!_isRecordingVoice)
                return;

            await _audioRecorder.StopAsync();
            _isRecordingVoice = false;
            VoiceButton.BackgroundColor = Color.FromArgb("#2A2A2A");
            VoiceButton.Text = "🎤";

            var duration = (int)Math.Round((DateTime.UtcNow - _recordingStartedAtUtc).TotalSeconds);
            _pendingVoiceDurationSeconds = Math.Max(1, duration);

            if (string.IsNullOrWhiteSpace(_pendingVoiceFilePath) || !File.Exists(_pendingVoiceFilePath))
            {
                _pendingVoiceDurationSeconds = null;
                await DisplayAlert("تنبيه", "تعذر حفظ الرسالة الصوتية.", "حسناً");
                return;
            }

            var fileInfo = new FileInfo(_pendingVoiceFilePath);
            if (fileInfo.Length <= 0)
            {
                DeletePendingVoiceFile();
                await DisplayAlert("تنبيه", "التسجيل الصوتي فارغ.", "حسناً");
                return;
            }

            StatusLabel.Text = $"تم حفظ رسالة صوتية ({FormatDuration(_pendingVoiceDurationSeconds)})";
            UpdateSendButtonText();
        }
        catch (Exception ex)
        {
            _isRecordingVoice = false;
            VoiceButton.BackgroundColor = Color.FromArgb("#2A2A2A");
            VoiceButton.Text = "🎤";
            await DisplayAlert("تنبيه", $"تعذر إيقاف التسجيل: {ex.Message}", "حسناً");
        }
    }

    private async void OnPlayVoiceClicked(object sender, EventArgs e)
    {
        try
        {
            if (_audioPlayer is not null && _audioPlayer.IsPlaying)
            {
                StopVoicePlayback();
                return;
            }

            if (sender is not Button button)
                return;

            if (button.CommandParameter is not long messageId)
                return;

            var message = _messages.FirstOrDefault(x =>
                x.Id == messageId &&
                x.IsVoice &&
                !string.IsNullOrWhiteSpace(x.VoiceUrl));

            if (message is null)
                return;

            StopVoicePlayback();

            _audioPlaybackStream = await _chatService.DownloadFileStreamAsync(message.VoiceUrl);

            if (_audioPlaybackStream is null)
            {
                await DisplayAlert("تنبيه", "تعذر تشغيل الرسالة الصوتية.", "حسناً");
                return;
            }

            _audioPlayer = AudioManager.Current.CreatePlayer(_audioPlaybackStream);
            _audioPlayer.Play();
        }
        catch (Exception ex)
        {
            await DisplayAlert("تنبيه", $"تعذر تشغيل الصوت: {ex.Message}", "حسناً");
        }
    }

    private async void OnMessageSelected(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            var selected = e.CurrentSelection?.FirstOrDefault() as ChatMessageViewModel;
            if (selected is null)
                return;

            MessagesCollection.SelectedItem = null;

            if (selected.UserId <= 0)
                return;

            await Navigation.PushAsync(new UserProfilePage(selected.UserId));
        }
        catch
        {
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await RefreshStateAsync();
    }

    private void StopVoicePlayback()
    {
        try
        {
            _audioPlayer?.Stop();
            _audioPlayer?.Dispose();
        }
        catch
        {
        }

        try
        {
            _audioPlaybackStream?.Dispose();
        }
        catch
        {
        }

        _audioPlayer = null;
        _audioPlaybackStream = null;
    }

    private void DeletePendingVoiceFile()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_pendingVoiceFilePath) && File.Exists(_pendingVoiceFilePath))
                File.Delete(_pendingVoiceFilePath);
        }
        catch
        {
        }

        _pendingVoiceFilePath = null;
        _pendingVoiceDurationSeconds = null;
        UpdateSendButtonText();
    }

    private void UpdateSendButtonText()
    {
        SendButton.Text = string.IsNullOrWhiteSpace(_pendingVoiceFilePath)
            ? "إرسال"
            : "إرسال صوت";
    }

    private static string FormatDuration(int? seconds)
    {
        if (!seconds.HasValue || seconds.Value <= 0)
            return "0:00";

        var ts = TimeSpan.FromSeconds(seconds.Value);
        return ts.ToString(@"m\:ss");
    }

    private async void OnVoicePressed(object sender, EventArgs e)
    {
        await StartVoiceRecordingAsync();
    }

    private async void OnVoiceReleased(object sender, EventArgs e)
    {
        await StopVoiceRecordingAsync();

        if (!string.IsNullOrWhiteSpace(_pendingVoiceFilePath))
            await SendPendingVoiceAsync();
    }

    private void ShowLiveShortcut()
    {
        LiveShortcutBorder.IsVisible = true;
    }

    private void HideLiveShortcut()
    {
        LiveShortcutBorder.IsVisible = false;
    }

    private async void OnOpenLiveShortcutTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new Features.Live.LivePage());
    }
}