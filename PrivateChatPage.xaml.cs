using System.Collections.ObjectModel;
using Plugin.Maui.Audio;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public partial class PrivateChatPage : ContentPage
{
    private const int CacheLimit = 100;
    private const int OlderPageSize = 40;

    private readonly MessengerService _messengerService = new();
    private readonly ChatService _chatService = new();
    private readonly PrivateMessageCacheService _cacheService = new();

    private readonly ObservableCollection<PrivateChatMessageViewModel> _messages = new();

    private readonly int _myMainUserId;
    private readonly int _targetMessengerUserId;
    private readonly int _targetMainAppUserId;
    private readonly string _targetUserName;

    private string _deviceId = "";
    private long _conversationId;
    private int _myMessengerUserId;
    private ChatUserDto? _mePublicProfile;
    private ChatUserDto? _targetPublicProfile;

    private bool _isInitialized;
    private bool _isPolling;
    private bool _isLoadingMessages;
    private bool _isLoadingOlder;
    private bool _isSending;
    private bool _hasMoreOlderMessages = true;

    private bool _isUserScrolling;
    private bool _isNearBottom = true;
    private DateTime _lastScrollAtUtc = DateTime.MinValue;

    private long? _oldestLoadedMessageId;
    private CancellationTokenSource? _pollingCts;

    private readonly IAudioRecorder _audioRecorder;
    private IAudioPlayer? _audioPlayer;
    private Stream? _audioPlaybackStream;

    private bool _isRecordingVoice;
    private string? _pendingVoiceFilePath;
    private int? _pendingVoiceDurationSeconds;
    private DateTime _recordingStartedAtUtc;

    public PrivateChatPage(int myMainUserId, int targetMessengerUserId, int targetMainAppUserId, string targetUserName)
    {
        InitializeComponent();

        _myMainUserId = myMainUserId;
        _targetMessengerUserId = targetMessengerUserId;
        _targetMainAppUserId = targetMainAppUserId;
        _targetUserName = targetUserName ?? "";

        _audioRecorder = AudioManager.Current.CreateRecorder();
        MessagesCollection.ItemsSource = _messages;
        MessagesCollection.Scrolled += OnMessagesScrolled;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await InitializeAsync();
            }

            StartPolling();

            await Task.Delay(150);
            await ForceScrollToBottomAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "حسناً");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPolling();
        StopVoicePlayback();
    }

    private async Task InitializeAsync()
    {
        StatusLabel.Text = "جاري التحميل...";

        _deviceId = _messengerService.GetOrCreateDeviceId();

        var chatUser = await _chatService.GetMeAsync(_deviceId);
        if (chatUser is null)
        {
            var savedName = _chatService.GetSavedName();
            if (string.IsNullOrWhiteSpace(savedName))
                savedName = $"User_{_deviceId[..Math.Min(5, _deviceId.Length)]}";

            chatUser = await _chatService.RegisterOrUpdateAsync(_deviceId, savedName);
        }

        if (chatUser is null || chatUser.Id <= 0)
            throw new Exception("تعذر جلب المستخدم الحالي.");

        _mePublicProfile = chatUser;

        var synced = await _messengerService.SyncUserAsync(
            chatUser.Id,
            _deviceId,
            string.IsNullOrWhiteSpace(chatUser.Name) ? $"User_{chatUser.Id}" : chatUser.Name.Trim(),
            chatUser.AvatarUrl ?? "");

        if (synced is null || synced.Id <= 0)
            throw new Exception("تعذر ربط المستخدم مع المسنجر.");

        _myMessengerUserId = synced.Id;

        if (_targetMainAppUserId > 0)
            _targetPublicProfile = await _chatService.GetProfileByIdAsync(_targetMainAppUserId);

        var conversation = await _messengerService.StartPrivateConversationAsync(_deviceId, _targetMessengerUserId);
        if (conversation is null || conversation.ConversationId <= 0)
            throw new Exception("تعذر فتح المحادثة الخاصة.");

        _conversationId = conversation.ConversationId;

        StatusLabel.Text = string.IsNullOrWhiteSpace(_targetUserName)
            ? "المحادثة الخاصة"
            : _targetUserName;

        var cached = await _cacheService.GetCachedMessagesAsync(_conversationId);
        if (cached.Count > 0)
        {
            ReplaceVisibleMessages(cached);
            await ForceScrollToBottomAsync();
        }

        await SyncLatestMessagesAsync(fullRefresh: true, scrollToBottom: true);

        _isNearBottom = true;
        _isUserScrolling = false;
        await ForceScrollToBottomAsync();
    }

    private async Task SyncLatestMessagesAsync(bool fullRefresh, bool scrollToBottom = false)
    {
        if (_isLoadingMessages || _conversationId <= 0 || string.IsNullOrWhiteSpace(_deviceId))
            return;

        try
        {
            _isLoadingMessages = true;

            var take = fullRefresh ? CacheLimit : 20;

            var latest = await _messengerService.GetMessagesAsync(
                _deviceId,
                _conversationId,
                take: take);

            if (latest is null || latest.Count == 0)
                return;

            latest = latest
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

            await _cacheService.SaveLatestMessagesAsync(_conversationId, latest);

            if (fullRefresh)
            {
                var currentOlder = _messages
                    .Where(x => latest.All(m => m.Id != x.Id))
                    .Select(ToDtoFromViewModel)
                    .OrderBy(x => x.CreatedAtUtc)
                    .ThenBy(x => x.Id)
                    .ToList();

                var merged = currentOlder
                    .Concat(latest)
                    .GroupBy(x => x.Id)
                    .Select(g => g.Last())
                    .OrderBy(x => x.CreatedAtUtc)
                    .ThenBy(x => x.Id)
                    .ToList();

                ReplaceVisibleMessages(merged);
            }
            else
            {
                MergeIncomingMessages(latest);
            }

            await TryMarkCurrentConversationReadAsync();

            if (scrollToBottom && ShouldAutoScrollToBottom())
                await ForceScrollToBottomAsync();
        }
        finally
        {
            _isLoadingMessages = false;
        }
    }

    private void MergeIncomingMessages(IEnumerable<PrivateMessageDto> incoming)
    {
        var changed = false;
        var map = _messages.ToDictionary(x => x.Id, x => x);

        foreach (var item in incoming.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id))
        {
            if (map.TryGetValue(item.Id, out var existing))
            {
                var newImageUrl = ResolveImageUrl(item);
                var newVoiceUrl = ResolveVoiceUrl(item);
                var newText = item.Text ?? "";
                var newType = item.Type?.Trim() ?? "Text";
                var newDuration = FormatDuration(item.DurationSeconds);

                if (existing.Text != newText ||
                    existing.Type != newType ||
                    existing.ImageUrl != newImageUrl ||
                    existing.VoiceUrl != newVoiceUrl ||
                    existing.VoiceDurationText != newDuration ||
                    existing.TimeText != item.CreatedAtUtc.ToLocalTime().ToString("hh:mm tt"))
                {
                    var updated = ToViewModel(item);
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

            var older = await _messengerService.GetMessagesAsync(
                _deviceId,
                _conversationId,
                take: OlderPageSize,
                beforeMessageId: _oldestLoadedMessageId.Value);

            if (older is null || older.Count == 0)
            {
                _hasMoreOlderMessages = false;
                return;
            }

            older = older
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

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

            for (int i = 0; i < itemsToInsert.Count; i++)
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

    private void ReplaceVisibleMessages(IEnumerable<PrivateMessageDto> items)
    {
        _messages.Clear();

        foreach (var item in items.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id))
            _messages.Add(ToViewModel(item));

        UpdateOldestLoadedMessageId();
        _hasMoreOlderMessages = _messages.Count >= CacheLimit;
    }

    private void UpdateOldestLoadedMessageId()
    {
        _oldestLoadedMessageId = _messages.Count == 0
            ? null
            : _messages.Min(x => x.Id);
    }

    private async Task TryMarkCurrentConversationReadAsync()
    {
        try
        {
            if (_messages.Count == 0)
                return;

            var lastId = _messages.Max(x => x.Id);
            await _messengerService.MarkConversationReadAsync(_deviceId, _conversationId, lastId);
        }
        catch
        {
        }
    }

    private void StartPolling()
    {
        if (_isPolling || _conversationId <= 0)
            return;

        _isPolling = true;
        _pollingCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            var secondsCounter = 0;

            try
            {
                while (_pollingCts is not null && !_pollingCts.IsCancellationRequested)
                {
                    try
                    {
                        secondsCounter++;
                        var fullRefresh = secondsCounter >= 30;

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await SyncLatestMessagesAsync(
                                fullRefresh,
                                scrollToBottom: ShouldAutoScrollToBottom());
                        });

                        if (fullRefresh)
                            secondsCounter = 0;
                    }
                    catch
                    {
                    }

                    await Task.Delay(1000, _pollingCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                _isPolling = false;
            }
        });
    }

    private void StopPolling()
    {
        try
        {
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
        }
        catch
        {
        }

        _pollingCts = null;
        _isPolling = false;
    }

    private void OnMessagesScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        _isUserScrolling = true;
        _lastScrollAtUtc = DateTime.UtcNow;

        _isNearBottom = IsNearBottom(e);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(700);

                if ((DateTime.UtcNow - _lastScrollAtUtc).TotalMilliseconds >= 650)
                    _isUserScrolling = false;
            }
            catch
            {
            }
        });

        if (e.FirstVisibleItemIndex <= 1)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadOlderMessagesAsync();
            });
        }
    }

    private bool IsNearBottom(ItemsViewScrolledEventArgs e)
    {
        if (_messages.Count == 0)
            return true;

        var lastVisibleIndex = e.LastVisibleItemIndex;
        return lastVisibleIndex >= _messages.Count - 3;
    }

    private bool ShouldAutoScrollToBottom()
    {
        return !_isUserScrolling && _isNearBottom;
    }

    private async Task ForceScrollToBottomAsync()
    {
        if (_messages.Count == 0)
            return;

        var lastItem = _messages[^1];

        for (int i = 0; i < 3; i++)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    MessagesCollection.ScrollTo(lastItem, position: ScrollToPosition.End, animate: false);
                }
                catch
                {
                }
            });

            await Task.Delay(120);
        }
    }

    private PrivateChatMessageViewModel ToViewModel(PrivateMessageDto dto)
    {
        var isMine = dto.SenderUserId == _myMessengerUserId;
        var type = dto.Type?.Trim() ?? "Text";

        var imageUrl = ResolveImageUrl(dto);
        var voiceUrl = ResolveVoiceUrl(dto);

        var senderName = isMine
            ? (_mePublicProfile?.Name ?? "أنت")
            : (_targetPublicProfile?.Name ?? _targetUserName ?? dto.SenderUserName);

        var avatarUrl = isMine
            ? ResolvePublicAvatar(_mePublicProfile?.AvatarUrl)
            : ResolvePublicAvatar(_targetPublicProfile?.AvatarUrl);

        return new PrivateChatMessageViewModel
        {
            Id = dto.Id,
            UserId = dto.SenderUserId,
            UserName = senderName,
            AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? "dotnet_bot.png" : avatarUrl,
            Text = dto.Text ?? "",
            TimeText = dto.CreatedAtUtc.ToLocalTime().ToString("hh:mm tt"),
            CreatedAtUtc = dto.CreatedAtUtc,
            Type = type,
            IsMine = isMine,
            IsImage = !string.IsNullOrWhiteSpace(imageUrl),
            ImageUrl = imageUrl,
            IsVoice = !string.IsNullOrWhiteSpace(voiceUrl),
            VoiceUrl = voiceUrl,
            VoiceDurationText = FormatDuration(dto.DurationSeconds),
            HasText = !string.IsNullOrWhiteSpace(dto.Text),
            BubbleColor = isMine ? Color.FromArgb("#E62B175A") : Color.FromArgb("#E6111111"),
            TextColor = Colors.White,
            ShowAvatar = true,
            ShowSenderName = true
        };
    }

    private string ResolveImageUrl(PrivateMessageDto dto)
    {
        var type = dto.Type?.Trim() ?? "Text";

        if (!type.Equals("Image", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(dto.FileUrl))
            return "";

        return _messengerService.FixFileUrl(dto.FileUrl);
    }

    private string ResolveVoiceUrl(PrivateMessageDto dto)
    {
        var type = dto.Type?.Trim() ?? "Text";

        if (!type.Equals("Voice", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(dto.FileUrl))
            return "";

        return _messengerService.FixFileUrl(dto.FileUrl);
    }

    private PrivateMessageDto ToDtoFromViewModel(PrivateChatMessageViewModel vm)
    {
        return new PrivateMessageDto
        {
            Id = vm.Id,
            ConversationId = _conversationId,
            SenderUserId = vm.UserId,
            SenderUserName = vm.UserName,
            SenderAvatarUrl = vm.AvatarUrl,
            Type = vm.Type,
            Text = vm.Text,
            FileUrl = vm.IsImage ? vm.ImageUrl : vm.IsVoice ? vm.VoiceUrl : null,
            DurationSeconds = ParseDuration(vm.VoiceDurationText),
            CreatedAtUtc = vm.CreatedAtUtc
        };
    }

    private string ResolvePublicAvatar(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return "";

        return _chatService.FixAvatarUrl(avatarUrl);
    }

    private async Task ScrollToBottomAsync()
    {
        await ForceScrollToBottomAsync();
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        if (_isSending || _conversationId <= 0)
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(_pendingVoiceFilePath))
            {
                await SendPendingVoiceAsync();
                return;
            }

            var text = MessageEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return;

            _isSending = true;
            SendButton.IsEnabled = false;

            var sent = await _messengerService.SendTextAsync(_deviceId, _conversationId, text);
            if (sent is null)
            {
                await DisplayAlert("تنبيه", "فشل إرسال الرسالة.", "حسناً");
                return;
            }

            MessageEntry.Text = "";
            _isNearBottom = true;
            await SyncLatestMessagesAsync(fullRefresh: false, scrollToBottom: true);
            await ForceScrollToBottomAsync();
        }
        finally
        {
            _isSending = false;
            SendButton.IsEnabled = true;
            UpdateSendButtonText();
        }
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        if (_isSending || _conversationId <= 0)
            return;

        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null)
                return;

            _isSending = true;
            ImageButton.IsEnabled = false;

            var sent = await _messengerService.SendImageAsync(
                _deviceId,
                _conversationId,
                photo,
                MessageEntry.Text?.Trim() ?? "");

            if (sent is null)
            {
                await DisplayAlert("تنبيه", "فشل إرسال الصورة.", "حسناً");
                return;
            }

            MessageEntry.Text = "";
            _isNearBottom = true;
            await SyncLatestMessagesAsync(fullRefresh: false, scrollToBottom: true);
            await ForceScrollToBottomAsync();
        }
        finally
        {
            _isSending = false;
            ImageButton.IsEnabled = true;
        }
    }

    private async Task StartVoiceRecordingAsync()
    {
        try
        {
            var permission = await Permissions.RequestAsync<Permissions.Microphone>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlert("تنبيه", "لم يتم منح صلاحية الميكروفون.", "حسناً");
                return;
            }

            DeletePendingVoiceFile();

            var fileName = $"pm_voice_{DateTime.UtcNow:yyyyMMdd_HHmmss}.wav";
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

    private async Task SendPendingVoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(_pendingVoiceFilePath))
            return;

        SendButton.IsEnabled = false;

        var sent = await _messengerService.SendVoiceAsync(
            _deviceId,
            _conversationId,
            _pendingVoiceFilePath,
            _pendingVoiceDurationSeconds);

        if (sent is null)
        {
            await DisplayAlert("تنبيه", "فشل إرسال الرسالة الصوتية.", "حسناً");
            return;
        }

        DeletePendingVoiceFile();
        StatusLabel.Text = string.IsNullOrWhiteSpace(_targetUserName) ? "المحادثة الخاصة" : _targetUserName;
        _isNearBottom = true;
        await SyncLatestMessagesAsync(fullRefresh: false, scrollToBottom: true);
        await ForceScrollToBottomAsync();
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

            _audioPlaybackStream = await _messengerService.DownloadFileStreamAsync(message.VoiceUrl);
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

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        _isNearBottom = true;
        _isUserScrolling = false;
        await SyncLatestMessagesAsync(fullRefresh: true, scrollToBottom: true);
        await ForceScrollToBottomAsync();
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

    private static int? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (TimeSpan.TryParseExact(value, @"m\:ss", null, out var ts))
            return (int)ts.TotalSeconds;

        return null;
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

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}

public class PrivateChatMessageViewModel
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Text { get; set; } = "";
    public string TimeText { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string Type { get; set; } = "";

    public bool IsMine { get; set; }
    public bool IsImage { get; set; }
    public string ImageUrl { get; set; } = "";

    public bool IsVoice { get; set; }
    public string VoiceUrl { get; set; } = "";
    public string VoiceDurationText { get; set; } = "";

    public bool HasText { get; set; }

    public Color BubbleColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.White;

    public bool ShowAvatar { get; set; } = true;
    public bool ShowSenderName { get; set; } = true;
}