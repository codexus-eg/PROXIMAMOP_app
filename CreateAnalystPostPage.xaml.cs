using PROXIMAMOP.Services;

namespace PROXIMAMOP.Pages;

public partial class CreateAnalystPostPage : ContentPage
{
    private readonly AnalystService _analystService = new();
    private readonly int _analystId;
    private FileResult? _selectedFile;
    private bool _isPublishing;

    public CreateAnalystPostPage(int analystId)
    {
        InitializeComponent();
        _analystId = analystId;
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "اختر صورة للمنشور"
            });

            if (file is null)
                return;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";

            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
            {
                await DisplayAlert("تنبيه", "صيغة الصورة غير مدعومة. استخدم jpg أو jpeg أو png أو webp.", "OK");
                return;
            }

            _selectedFile = file;
            SelectedFileLabel.Text = file.FileName;

            await using var stream = await file.OpenReadAsync();
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;

            PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(memory.ToArray()));
            PreviewImage.IsVisible = true;
            PreviewBorder.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("تنبيه", $"تعذر اختيار الصورة: {ex.Message}", "OK");
        }
    }

    private void OnRemoveImageClicked(object sender, EventArgs e)
    {
        _selectedFile = null;
        SelectedFileLabel.Text = "لم يتم اختيار صورة";
        PreviewImage.Source = null;
        PreviewImage.IsVisible = false;
        PreviewBorder.IsVisible = false;
    }

    private async void OnPublishClicked(object sender, EventArgs e)
    {
        if (_isPublishing)
            return;

        var text = PostTextEditor.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(text) && _selectedFile is null)
        {
            await DisplayAlert("تنبيه", "اكتب نص أو اختر صورة على الأقل.", "OK");
            return;
        }

        try
        {
            _isPublishing = true;
            PublishButton.IsEnabled = false;
            PublishButton.Text = "جاري النشر";

            var deviceId = _analystService.GetOrCreateDeviceId();
            var result = await _analystService.CreatePostAsync(deviceId, text, _selectedFile);

            if (result is null)
            {
                await DisplayAlert("تنبيه", "تعذر نشر المنشور.", "OK");
                return;
            }

            await DisplayAlert("نجاح", "تم نشر المنشور بنجاح.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("تنبيه", $"حصل خطأ أثناء النشر: {ex.Message}", "OK");
        }
        finally
        {
            _isPublishing = false;
            PublishButton.IsEnabled = true;
            PublishButton.Text = "نشر";
        }
    }
}