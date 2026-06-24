using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PROXIMAMOP.Models;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public partial class MessagesPage : ContentPage, INotifyPropertyChanged
{
    private readonly MessengerService _messengerService = new();
    private readonly ChatService _chatService = new();
    private readonly ObservableCollection<MessengerConversationItemViewModel> _allConversations = new();
    private readonly Dictionary<int, ChatUserDto?> _publicProfileCache = new();

    private ChatUserDto? _currentChatUser;

    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private string _errorMessage = "";
    private string _headerSubtitle = "Your private conversations";
    private string _searchText = "";

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MessengerConversationItemViewModel> Conversations { get; } = new();

    public Command RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowList));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError == value) return;
            _hasError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowList));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public string HeaderSubtitle
    {
        get => _headerSubtitle;
        set
        {
            if (_headerSubtitle == value) return;
            _headerSubtitle = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public bool ShowEmptyState => !IsLoading && !HasError && Conversations.Count == 0;
    public bool ShowList => !IsLoading && !HasError && Conversations.Count > 0;

    public MessagesPage()
    {
        InitializeComponent();

        BindingContext = this;

        RefreshCommand = new Command(async () => await RefreshAsync());

        Conversations.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowList));
            OnPropertyChanged(nameof(ShowEmptyState));

            HeaderSubtitle = Conversations.Count == 0
                ? "No conversations yet"
                : $"{Conversations.Count} conversation(s)";
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!IsLoading)
            await RefreshAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = "";

            var currentUser = await EnsureMessengerUserSyncedAsync();

            if (currentUser is null)
            {
                HasError = true;
                ErrorMessage = "Unable to sync current messenger user.";
                return;
            }

            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            HasError = false;
            ErrorMessage = "";

            var currentUser = await EnsureMessengerUserSyncedAsync();

            if (currentUser is null)
            {
                HasError = true;
                ErrorMessage = "Unable to sync current messenger user.";
                return;
            }

            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
            IsLoading = false;
        }
    }

    private async Task LoadConversationsAsync()
    {
        var deviceId = _messengerService.GetOrCreateDeviceId();
        var data = await _messengerService.GetConversationsAsync(deviceId);

        _allConversations.Clear();

        var ordered = data
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToList();

        foreach (var item in ordered)
        {
            var vm = await ToViewModelAsync(item);
            _allConversations.Add(vm);
        }

        ApplySearch();
    }

    private async Task<MessengerUserDto?> EnsureMessengerUserSyncedAsync()
    {
        var deviceId = _chatService.GetOrCreateDeviceId();

        var chatUser = await _chatService.GetMeAsync(deviceId);

        if (chatUser is null)
        {
            var name = _chatService.GetSavedName();

            if (string.IsNullOrWhiteSpace(name))
                name = $"User_{deviceId[..Math.Min(5, deviceId.Length)]}";

            chatUser = await _chatService.RegisterOrUpdateAsync(deviceId, name);
        }

        if (chatUser is null)
            throw new Exception("Chat server did not return current user.");

        if (chatUser.Id <= 0)
            throw new Exception("Chat user id is invalid.");

        _currentChatUser = chatUser;

        var userName = string.IsNullOrWhiteSpace(chatUser.Name)
            ? $"User_{chatUser.Id}"
            : chatUser.Name.Trim();

        var avatarUrl = chatUser.AvatarUrl ?? "";

        return await _messengerService.SyncUserAsync(
            chatUser.Id,
            deviceId,
            userName,
            avatarUrl);
    }

    private async Task<MessengerConversationItemViewModel> ToViewModelAsync(ConversationListItemDto dto)
    {
        var date = dto.LastMessageAtUtc ?? dto.CreatedAtUtc;

        var publicProfile = await GetPublicProfileAsync(dto.OtherMainAppUserId);

        var resolvedName = !string.IsNullOrWhiteSpace(publicProfile?.Name)
            ? publicProfile!.Name
            : (string.IsNullOrWhiteSpace(dto.OtherUserName) ? "Unknown user" : dto.OtherUserName);

        var resolvedAvatar = ResolveConversationAvatarUrl(dto.OtherAvatarUrl, publicProfile?.AvatarUrl);

        return new MessengerConversationItemViewModel
        {
            ConversationId = dto.ConversationId,
            UserId = dto.OtherUserId,
            MainAppUserId = dto.OtherMainAppUserId,
            UserName = resolvedName,
            AvatarUrl = resolvedAvatar,
            ShowImageAvatar = !string.IsNullOrWhiteSpace(resolvedAvatar),
            ShowInitialAvatar = string.IsNullOrWhiteSpace(resolvedAvatar),
            LastMessagePreview = BuildLastMessagePreview(dto),
            LastMessageTimeText = FormatTime(date),
            UnreadCount = dto.UnreadCount,
            ShowUnreadCount = dto.UnreadCount > 0,
            UnreadCountText = dto.UnreadCount > 99 ? "99+" : dto.UnreadCount.ToString(),
            ShowOnlineDot = false,
            OnlineDotColor = Color.FromArgb("#22C55E"),
            SortDateUtc = date
        };
    }

    private async Task<ChatUserDto?> GetPublicProfileAsync(int mainAppUserId)
    {
        if (mainAppUserId <= 0)
            return null;

        if (_publicProfileCache.TryGetValue(mainAppUserId, out var cached))
            return cached;
        try
        {
            var profile = await _chatService.GetProfileByIdAsync(mainAppUserId);
            _publicProfileCache[mainAppUserId] = profile;
            return profile;
        }
        catch
        {
            _publicProfileCache[mainAppUserId] = null;
            return null;
        }
    }

    private string ResolveConversationAvatarUrl(string? messengerAvatarUrl, string? publicAvatarUrl)
    {
        var raw = !string.IsNullOrWhiteSpace(publicAvatarUrl)
            ? publicAvatarUrl!
            : (messengerAvatarUrl ?? "");

        if (string.IsNullOrWhiteSpace(raw))
            return "";

        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        return _chatService.FixAvatarUrl(raw);
    }

    private string BuildLastMessagePreview(ConversationListItemDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.LastMessageText))
            return dto.LastMessageText!;

        if (!string.IsNullOrWhiteSpace(dto.LastMessageType))
        {
            return dto.LastMessageType.Trim().ToLowerInvariant() switch
            {
                "image" => "📷 Image",
                "voice" => "🎤 Voice message",
                "text" => "Message",
                _ => "Message"
            };
        }

        return "No messages yet";
    }

    private static string FormatTime(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        var now = DateTime.Now;

        if (local.Date == now.Date)
            return local.ToString("hh:mm tt");

        if (local.Date == now.Date.AddDays(-1))
            return "Yesterday";

        if ((now.Date - local.Date).TotalDays < 7)
            return local.ToString("ddd");

        return local.ToString("dd/MM");
    }

    private void ApplySearch()
    {
        var q = (SearchText ?? string.Empty).Trim();

        Conversations.Clear();

        IEnumerable<MessengerConversationItemViewModel> filtered = _allConversations;

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(x =>
                x.UserName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.LastMessagePreview.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.UserId.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.MainAppUserId.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in filtered.OrderByDescending(x => x.SortDateUtc))
            Conversations.Add(item);

        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var selected = e.CurrentSelection.FirstOrDefault() as MessengerConversationItemViewModel;
            if (selected is null)
                return;

            if (ChatsList is not null)
                ChatsList.SelectedItem = null;

            if (_currentChatUser is null)
                await EnsureMessengerUserSyncedAsync();

            if (_currentChatUser is null)
            {
                await DisplayAlert("Error", "Current user not found.", "OK");
                return;
            }

            await Navigation.PushAsync(
                new PrivateChatPage(
                    _currentChatUser.Id,
                    selected.UserId,
                    selected.MainAppUserId,
                    selected.UserName));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnRetryClicked(object sender, EventArgs e)
    {
        await LoadAsync();
    }

    private async void OnRefreshTapped(object? sender, TappedEventArgs e)
    {
        await RefreshAsync();
    }
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = e.NewTextValue ?? "";
        ApplySearch();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async void OnStartNewChatTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            HasError = false;
            ErrorMessage = "";

            var syncedUser = await EnsureMessengerUserSyncedAsync();
            if (syncedUser is null)
            {
                await DisplayAlert("Error", "Unable to sync current messenger user.", "OK");
                return;
            }

            if (_currentChatUser is null)
            {
                await DisplayAlert("Error", "Current user not found.", "OK");
                return;
            }

            var query = await DisplayPromptAsync(
                "Start New Chat",
                "Type user name",
                accept: "Search",
                cancel: "Cancel",
                placeholder: "Search user...");

            if (string.IsNullOrWhiteSpace(query))
                return;

            var deviceId = _messengerService.GetOrCreateDeviceId();
            var users = await _messengerService.SearchUsersAsync(deviceId, query.Trim(), 20);

            users = users
                .Where(x => x.Id > 0 && x.Id != syncedUser.Id)
                .OrderBy(x => x.UserName)
                .ToList();

            if (users.Count == 0)
            {
                await DisplayAlert("No Results", "No users found.", "OK");
                return;
            }

            var options = users
                .Take(10)
                .Select(x => $"{x.UserName}  (#{x.Id})")
                .ToArray();

            var selectedText = await DisplayActionSheet(
                "Choose user",
                "Cancel",
                null,
                options);

            if (string.IsNullOrWhiteSpace(selectedText) || selectedText == "Cancel")
                return;

            var selectedUser = users.FirstOrDefault(x => $"{x.UserName}  (#{x.Id})" == selectedText);
            if (selectedUser is null)
            {
                await DisplayAlert("Error", "Selected user not found.", "OK");
                return;
            }

            var conversation = await _messengerService.StartPrivateConversationAsync(deviceId, selectedUser.Id);
            if (conversation is null)
            {
                await DisplayAlert("Error", "Could not start conversation.", "OK");
                return;
            }

            SearchText = "";
            await RefreshAsync();

            await Navigation.PushAsync(
                new PrivateChatPage(
                    _currentChatUser.Id,
                    selectedUser.Id,
                    selectedUser.MainAppUserId,
                    selectedUser.UserName));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        await Task.CompletedTask;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MessengerConversationItemViewModel
{
    public long ConversationId { get; set; }
    public int UserId { get; set; }
    public int MainAppUserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public bool ShowImageAvatar { get; set; }
    public bool ShowInitialAvatar { get; set; }
    public string LastMessagePreview { get; set; } = "";
    public string LastMessageTimeText { get; set; } = "";
    public int UnreadCount { get; set; }
    public bool ShowUnreadCount { get; set; }
    public string UnreadCountText { get; set; } = "";
    public bool ShowOnlineDot { get; set; }
    public Color OnlineDotColor { get; set; } = Color.FromArgb("#22C55E");
    public DateTime SortDateUtc { get; set; }

    public string Initial
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UserName))
                return "?";

            return UserName.Trim()[0].ToString().ToUpperInvariant();
        }
    }
}