using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using PROXIMAMOP.Services;
using PROXIMAMOP.Services.Live;
using System.Net.Http.Json;

namespace PROXIMAMOP.Features.Live;

public partial class LivePage : ContentPage
{
    private readonly LiveTokenService _liveTokenService;
    private readonly ILiveInAppBrowserService _liveInAppBrowserService;
    private readonly HttpClient _httpClient;

    private bool _isLoading;
    private string? _currentJoinUrl;
    private string? _currentRole;

    private const string DefaultRoomName = "test";
    private const string DefaultRequestedRole = "master";

    public LivePage()
    {
        InitializeComponent();

        _liveTokenService = new LiveTokenService();
        _liveInAppBrowserService = ServiceHelper.Services.GetRequiredService<ILiveInAppBrowserService>();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        RoomLabel.Text = $"Room: {DefaultRoomName}";
        RoleLabel.Text = "Role: -";
        StatusLabel.Text = "جاهز";
        InfoLabel.Text = "اضغط دخول البث حتى يفتح داخل نافذة داخلية.";
        SetButtonsEnabled(true);
        UpdateOpenButtonState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await PrepareLiveJoinUrlAsync();
    }

    private async Task PrepareLiveJoinUrlAsync()
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            SetButtonsEnabled(false);
            StatusLabel.Text = "جارِ تجهيز رابط البث...";
            InfoLabel.Text = "جارِ التحقق من الصلاحيات والرابط...";

            var userId = await GetOrCreateDeviceIdAsync();

            var result = await _liveTokenService.CreateJoinUrlAsync(
                roomName: DefaultRoomName,
                userId: userId,
                displayName: userId,
                requestedRole: DefaultRequestedRole);

            if (!result.IsSuccess)
            {
                _currentJoinUrl = null;
                _currentRole = null;
                RoleLabel.Text = "Role: -";
                StatusLabel.Text = "فشل التحضير";
                InfoLabel.Text = result.ErrorMessage;
                UpdateOpenButtonState();
                return;
            }

            _currentJoinUrl = result.JoinUrl;
            _currentRole = result.Role;
            RoleLabel.Text = $"Role: {_currentRole}";
            StatusLabel.Text = "جاهز";
            InfoLabel.Text = "تم تجهيز رابط البث. اضغط دخول البث.";
            UpdateOpenButtonState();
        }
        catch (Exception ex)
        {
            _currentJoinUrl = null;
            _currentRole = null;
            RoleLabel.Text = "Role: -";
            StatusLabel.Text = "خطأ";
            InfoLabel.Text = ex.Message;
            UpdateOpenButtonState();
        }
        finally
        {
            _isLoading = false;
            SetButtonsEnabled(true);
        }
    }

    private async void OnOpenLiveClicked(object sender, EventArgs e)
    {
        if (_isLoading)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(_currentJoinUrl))
                await PrepareLiveJoinUrlAsync();

            if (string.IsNullOrWhiteSpace(_currentJoinUrl))
            {
                await DisplayAlert("Error", "رابط البث غير جاهز.", "OK");
                return;
            }

            await _liveInAppBrowserService.OpenAsync(_currentJoinUrl);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnStartLiveClicked(object sender, EventArgs e)
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            SetButtonsEnabled(false);
            StatusLabel.Text = "جارِ تشغيل البث...";

            var userId = await GetOrCreateDeviceIdAsync();
            var body = new
            {
                roomName = DefaultRoomName,
                hostUserId = userId,
                title = "Live"
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "https://streamflowapp.com/live/sessions/start",
                body);

            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                StatusLabel.Text = "فشل التشغيل";
                await DisplayAlert(
                    "Error",
                    $"فشل تشغيل البث: {(int)response.StatusCode}\n{raw}",
                    "OK");
                return;
            }

            StatusLabel.Text = "البث شغال";
            InfoLabel.Text = "تم تشغيل البث بنجاح. اضغط دخول البث.";
            await DisplayAlert("Success", "تم تشغيل البث بنجاح.", "OK");
            await PrepareLiveJoinUrlAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "خطأ";
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _isLoading = false;
            SetButtonsEnabled(true);
        }
    }

    private async void OnStopLiveClicked(object sender, EventArgs e)
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            SetButtonsEnabled(false);
            StatusLabel.Text = "جارِ إيقاف البث...";

            var userId = await GetOrCreateDeviceIdAsync();

            var body = new
            {
                roomName = DefaultRoomName,
                actorUserId = userId
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "https://streamflowapp.com/live/sessions/stop",
                body);

            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                StatusLabel.Text = "فشل الإيقاف";
                await DisplayAlert(
                    "Error",
                    $"فشل إيقاف البث: {(int)response.StatusCode}\n{raw}",
                    "OK");
                return;
            }

            StatusLabel.Text = "تم إيقاف البث";
            InfoLabel.Text = "تم إيقاف البث بنجاح.";
            await DisplayAlert("Success", "تم إيقاف البث بنجاح.", "OK");
            await PrepareLiveJoinUrlAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "خطأ";
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _isLoading = false;
            SetButtonsEnabled(true);
        }
    }

    private async Task<string> GetOrCreateDeviceIdAsync()
    {
        var deviceId = await SecureStorage.GetAsync("device_id");

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Guid.NewGuid().ToString("N");
            await SecureStorage.SetAsync("device_id", deviceId);
        }

        return deviceId;
    }

    private async void OnDeviceIdClicked(object sender, EventArgs e)
    {
        try
        {
            var deviceId = await GetOrCreateDeviceIdAsync();
            await Clipboard.SetTextAsync(deviceId);

            await DisplayAlert(
                "Device ID",
                $"هذا هو Device ID:\n\n{deviceId}\n\nتم نسخه إلى الحافظة.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.ToString(), "OK");
        }
    }

    private void SetButtonsEnabled(bool isEnabled)
    {
        if (StartLiveButton is not null)
            StartLiveButton.IsEnabled = isEnabled;

        if (StopLiveButton is not null)
            StopLiveButton.IsEnabled = isEnabled;

        if (OpenLiveButton is not null)
            OpenLiveButton.IsEnabled = isEnabled && !string.IsNullOrWhiteSpace(_currentJoinUrl);
    }

    private void UpdateOpenButtonState()
    {
        if (OpenLiveButton is null)
            return;
        OpenLiveButton.IsEnabled = !_isLoading && !string.IsNullOrWhiteSpace(_currentJoinUrl);
    }
}