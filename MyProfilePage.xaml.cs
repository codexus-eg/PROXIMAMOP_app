using PROXIMAMOP.Services;

namespace PROXIMAMOP;

public partial class MyProfilePage : ContentPage
{
    private readonly ChatService _chatService = new();
    private string _deviceId = "";

    public MyProfilePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        _deviceId = _chatService.GetOrCreateDeviceId();

        var me = await _chatService.GetMeAsync(_deviceId);
        if (me is null)
        {
            await DisplayAlert("خطأ", "تعذر تحميل الملف الشخصي.", "حسناً");
            return;
        }

        NameEntry.Text = me.Name;
        BioEditor.Text = me.Bio ?? "";
        AvatarImage.Source = string.IsNullOrWhiteSpace(me.AvatarUrl)
            ? "dotnet_bot.png"
            : _chatService.FixAvatarUrl(me.AvatarUrl);

        // 🔥 الجديد: عرض ID
        UserIdLabel.Text = $"ID: {me.Id}";

        ApplyBadge(me.BadgeType);
        InfoLabel.Text = $"Role: {me.Role}";
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

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? "";
        var bio = BioEditor.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("تنبيه", "اكتب الاسم أولاً.", "حسناً");
            return;
        }

        SaveButton.IsEnabled = false;

        try
        {
            var updated = await _chatService.UpdateProfileAsync(_deviceId, name, bio);
            if (updated is null)
            {
                await DisplayAlert("خطأ", "فشل حفظ الملف الشخصي.", "حسناً");
                return;
            }

            await DisplayAlert("تم", "تم حفظ الملف الشخصي بنجاح.", "حسناً");
            await LoadProfileAsync();
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnChangeAvatarClicked(object sender, EventArgs e)
    {
        try
        {
            var photo = await MediaPicker.PickPhotoAsync();
            if (photo is null)
                return;

            var updated = await _chatService.UploadAvatarAsync(_deviceId, photo);
            if (updated is null)
            {
                await DisplayAlert("خطأ", "فشل رفع الصورة.", "حسناً");
                return;
            }

            await LoadProfileAsync();
        }
        catch
        {
            await DisplayAlert("خطأ", "تعذر اختيار الصورة.", "حسناً");
        }
    }
}