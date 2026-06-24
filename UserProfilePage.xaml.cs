using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class UserProfilePage : ContentPage
{
    private readonly ChatService _chatService = new();
    private readonly int _userId;

    public UserProfilePage(int userId)
    {
        InitializeComponent();
        _userId = userId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        var user = await _chatService.GetProfileByIdAsync(_userId);
        if (user is null)
        {
            await DisplayAlert("خطأ", "تعذر تحميل ملف المستخدم.", "حسناً");
            await Navigation.PopAsync();
            return;
        }

        NameLabel.Text = user.Name;

        // 🔥 الجديد: عرض ID
        UserIdLabel.Text = $"ID: {user.Id}";

        RoleLabel.Text = $"Role: {user.Role}";
        BioLabel.Text = string.IsNullOrWhiteSpace(user.Bio) ? "No bio available." : user.Bio;

        AvatarImage.Source = string.IsNullOrWhiteSpace(user.AvatarUrl)
            ? "dotnet_bot.png"
            : _chatService.FixAvatarUrl(user.AvatarUrl);

        ApplyBadge(user.BadgeType);
    }

    private void ApplyBadge(string badgeType)
    {
        AvatarBorder.Stroke = _chatService.GetBadgeBorderColor(badgeType);
        BadgeIconBorder.BackgroundColor = _chatService.GetBadgeBackgroundColor(badgeType);
        BadgeIconLabel.TextColor = _chatService.GetBadgeIconTextColor(badgeType);
        BadgeIconLabel.Text = _chatService.GetBadgeIcon(badgeType);
        BadgeIconBorder.IsVisible = _chatService.HasBadge(badgeType);
        BadgeNameLabel.Text = _chatService.GetBadgeDisplayName(badgeType);
    }
}