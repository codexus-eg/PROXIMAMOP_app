using Microsoft.Extensions.DependencyInjection;
using PROXIMAMOP.Pages;
using PROXIMAMOP.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace PROXIMAMOP;

public partial class MainPage : ContentPage
{
    private const string BaseUrl = "http://195.3.223.75:5003";

    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<AdViewItem> _visualAds = [];

    private AudioAdItem? _audioAd;
    private bool _audioLoaded;

    private IAudioPlaybackService? AudioPlaybackService =>
        ServiceHelper.Services?.GetService<IAudioPlaybackService>();

    public MainPage()
    {
        InitializeComponent();

        AdsCarousel.ItemsSource = _visualAds;

        PlayAudioButton.IsEnabled = false;
        StopAudioButton.IsEnabled = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAdsAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        var audioService = AudioPlaybackService;

        if (audioService is not null)
        {
            try
            {
                await audioService.StopAsync();
            }
            catch
            {
            }
        }
    }

    private async Task LoadAdsAsync()
    {
        try
        {
            AdsStatusLabel.Text = "Loading";
            AdsCountLabel.Text = "0";
            EmptyAdsView.IsVisible = false;
            AdsCarousel.IsVisible = true;

            await LoadVisualAdsAsync();
            await LoadAudioAdAsync();

            AdsCountLabel.Text = _visualAds.Count.ToString();
            AdsStatusLabel.Text = _visualAds.Count > 0 ? "Ready" : "Empty";
        }
        catch (Exception ex)
        {
            _visualAds.Clear();
            EmptyAdsView.IsVisible = true;
            AdsCarousel.IsVisible = false;

            AdsStatusLabel.Text = "Error";
            AdsCountLabel.Text = "0";

            AudioTitleLabel.Text = "Featured Audio";
            AudioDescriptionLabel.Text = "Unable to load audio.";
            PlayAudioButton.IsEnabled = false;
            StopAudioButton.IsEnabled = false;

            _audioLoaded = false;
            _audioAd = null;

            try
            {
                var audioService = AudioPlaybackService;
                if (audioService is not null)
                {
                    await audioService.StopAsync();
                }
            }
            catch
            {
            }

            await DisplayAlertAsync("Error", $"Failed to load offers.\n\n{ex.Message}", "OK");
        }
    }

    private async Task LoadVisualAdsAsync()
    {
        _visualAds.Clear();

        var json = await _httpClient.GetStringAsync($"{BaseUrl}/api/ads/visual");

        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");

        foreach (var item in items.EnumerateArray())
        {
            var mediaType = item.TryGetProperty("mediaType", out var mediaTypeElement)
                ? mediaTypeElement.GetString() ?? string.Empty
                : string.Empty;

            var title = item.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString() ?? "Advertisement"
                : "Advertisement";

            var description = item.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? string.Empty
                : string.Empty;

            var buttonText = item.TryGetProperty("buttonText", out var buttonTextElement)
                ? buttonTextElement.GetString() ?? string.Empty
                : string.Empty;

            var buttonUrl = item.TryGetProperty("buttonUrl", out var buttonUrlElement)
                ? buttonUrlElement.GetString() ?? string.Empty
                : string.Empty;

            var fileUrl = item.TryGetProperty("fileUrl", out var fileUrlElement)
                ? fileUrlElement.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(fileUrl))
                continue;

            var viewItem = new AdViewItem
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Advertisement" : title,
                Description = description,
                ButtonText = string.IsNullOrWhiteSpace(buttonText) ? "Open" : buttonText,
                ButtonUrl = buttonUrl,
                HasButton = !string.IsNullOrWhiteSpace(buttonUrl)
            };

            if (string.Equals(mediaType, "Image", StringComparison.OrdinalIgnoreCase))
            {
                viewItem.IsImage = true;
                viewItem.IsVideo = false;
                viewItem.ImageSource = fileUrl;
            }
            else if (string.Equals(mediaType, "Video", StringComparison.OrdinalIgnoreCase))
            {
                viewItem.IsImage = false;
                viewItem.IsVideo = true;
                viewItem.VideoHtml = new HtmlWebViewSource
                {
                    Html = BuildVideoHtml(fileUrl)
                };
            }
            else
            {
                continue;
            }

            _visualAds.Add(viewItem);
        }

        EmptyAdsView.IsVisible = _visualAds.Count == 0;
        AdsCarousel.IsVisible = _visualAds.Count > 0;
    }

    private async Task LoadAudioAdAsync()
    {
        _audioLoaded = false;
        _audioAd = null;

        var audioService = AudioPlaybackService;
        if (audioService is not null)
        {
            try
            {
                await audioService.StopAsync();
            }
            catch
            {
            }
        }

        var json = await _httpClient.GetStringAsync($"{BaseUrl}/api/ads/audio");

        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");

        if (items.GetArrayLength() == 0)
        {
            AudioTitleLabel.Text = "Featured Audio";
            AudioDescriptionLabel.Text = "No audio available.";
            PlayAudioButton.IsEnabled = false;
            StopAudioButton.IsEnabled = false;

            if (AudioWebView is not null)
            {
                AudioWebView.Source = new HtmlWebViewSource
                {
                    Html = BuildAudioHtml(string.Empty)
                };
            }

            return;
        }

        var firstItem = items.EnumerateArray().First();

        var title = firstItem.TryGetProperty("title", out var titleElement)
            ? titleElement.GetString() ?? "Featured Audio"
            : "Featured Audio";

        var description = firstItem.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString() ?? string.Empty
            : string.Empty;

        var fileUrl = firstItem.TryGetProperty("fileUrl", out var fileUrlElement)
            ? fileUrlElement.GetString() ?? string.Empty
            : string.Empty;

        var buttonText = firstItem.TryGetProperty("buttonText", out var buttonTextElement)
            ? buttonTextElement.GetString()
            : null;

        var buttonUrl = firstItem.TryGetProperty("buttonUrl", out var buttonUrlElement)
            ? buttonUrlElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            AudioTitleLabel.Text = "Featured Audio";
            AudioDescriptionLabel.Text = "No audio available.";
            PlayAudioButton.IsEnabled = false;
            StopAudioButton.IsEnabled = false;

            if (AudioWebView is not null)
            {
                AudioWebView.Source = new HtmlWebViewSource
                {
                    Html = BuildAudioHtml(string.Empty)
                };
            }

            return;
        }

        _audioAd = new AudioAdItem
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Featured Audio" : title,
            Description = string.IsNullOrWhiteSpace(description) ? "Tap play to start audio." : description,
            FileUrl = fileUrl,
            ButtonText = buttonText,
            ButtonUrl = buttonUrl
        };

        AudioTitleLabel.Text = _audioAd.Title;
        AudioDescriptionLabel.Text = _audioAd.Description;

        if (AudioWebView is not null)
        {
            AudioWebView.Source = new HtmlWebViewSource
            {
                Html = BuildAudioHtml(_audioAd.FileUrl)
            };
        }

        _audioLoaded = true;
        PlayAudioButton.IsEnabled = audioService is not null;
        StopAudioButton.IsEnabled = audioService is not null;
    }

    private async void OnPlayAudioClicked(object sender, EventArgs e)
    {
        var audioService = AudioPlaybackService;

        if (!_audioLoaded || _audioAd is null || audioService is null || string.IsNullOrWhiteSpace(_audioAd.FileUrl))
            return;

        try
        {
            await audioService.PlayAsync(_audioAd.FileUrl);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Unable to play audio.\n\n{ex.Message}", "OK");
        }
    }

    private async void OnStopAudioClicked(object sender, EventArgs e)
    {
        var audioService = AudioPlaybackService;

        if (audioService is null)
            return;

        try
        {
            await audioService.StopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Unable to stop audio.\n\n{ex.Message}", "OK");
        }
    }

    private async void OnAdButtonClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.BindingContext is not AdViewItem item)
            return;

        if (string.IsNullOrWhiteSpace(item.ButtonUrl))
            return;

        try
        {
            await Launcher.Default.OpenAsync(item.ButtonUrl);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Unable to open link.\n\n{ex.Message}", "OK");
        }
    }

    private async void OnMessagesClicked(object sender, TappedEventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new MessagesPage());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Messages page failed.\n\n{ex.Message}", "OK");
        }
    }

    private async void OnSettingsClicked(object sender, TappedEventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new SettingsPage());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Settings page failed.\n\n{ex.Message}", "OK");
        }
    }

    private async void OnServicesClicked(object sender, TappedEventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new ServicesPage());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Services page failed.\n\n{ex.Message}", "OK");
        }
    }
    private async void OnRoomClicked(object sender, TappedEventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new RoomsPage());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Rooms page failed.\n\n{ex.Message}", "OK");
        }
    }
    private static string BuildVideoHtml(string url)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0"" />
<style>
html, body {{
    margin: 0;
    padding: 0;
    background: #0b121a;
    width: 100%;
    height: 100%;
    overflow: hidden;
}}
video {{
    width: 100%;
    height: 100%;
    object-fit: cover;
    background: #0b121a;
}}
</style>
</head>
<body>
<video controls playsinline preload=""metadata"">
  <source src=""{url}"" type=""video/mp4"">
</video>
</body>
</html>";
    }

    private static string BuildAudioHtml(string url)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
</head>
<body style=""margin:0;padding:0;background:#000;"">
    <audio id=""featuredAudio"" preload=""auto"">
        <source src=""{url}"" type=""audio/mpeg"">
    </audio>

    <script>
        function playAudio() {{
            var audio = document.getElementById('featuredAudio');
            if (audio) {{
                audio.play();
            }}
        }}

        function stopAudio() {{
            var audio = document.getElementById('featuredAudio');
            if (audio) {{
                audio.pause();
                audio.currentTime = 0;
            }}
        }}
    </script>
</body>
</html>";
    }

    private async Task DisplayAlertAsync(string title, string message, string cancel)
    {
        await MainThread.InvokeOnMainThreadAsync(() => DisplayAlert(title, message, cancel));
    }

    private sealed class AdViewItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ButtonText { get; set; } = "Open";
        public string ButtonUrl { get; set; } = string.Empty;
        public bool HasButton { get; set; }
        public bool IsImage { get; set; }
        public bool IsVideo { get; set; }
        public string ImageSource { get; set; } = string.Empty;
        public HtmlWebViewSource? VideoHtml { get; set; }
    }

    private sealed class AudioAdItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
    }
}