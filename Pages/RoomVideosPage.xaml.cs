using System.Collections.ObjectModel;
using System.Text.Json;

namespace PROXIMAMOP.Pages;

public partial class RoomVideosPage : ContentPage
{
    private const string RoomApi = "http://193.34.213.124:7070";

    private readonly HttpClient _http = new();
    private readonly ObservableCollection<VideoItem> _videos = [];

    private readonly int _roomId;
    private readonly string _roomName;
    private readonly string _password;

    public RoomVideosPage(int roomId, string roomName, string password)
    {
        InitializeComponent();

        _roomId = roomId;
        _roomName = roomName;
        _password = password;

        RoomTitleLabel.Text = roomName;
        VideosList.ItemsSource = _videos;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadVideosAsync();
    }

    private async Task LoadVideosAsync()
    {
        try
        {
            _videos.Clear();

            var url = $"{RoomApi}/api/rooms/{_roomId}/videos?password={Uri.EscapeDataString(_password)}";
            var json = await _http.GetStringAsync(url);

            var videos = JsonSerializer.Deserialize<List<VideoDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            foreach (var v in videos)
            {
                _videos.Add(new VideoItem
                {
                    Id = v.Id,
                    Title = v.Title,
                    Url = v.Url,
                    CreatedAt = v.CreatedAt,
                    VideoHtml = new HtmlWebViewSource
                    {
                        Html = BuildVideoHtml(v.Url)
                    }
                });
            }

            EmptyLabel.IsVisible = _videos.Count == 0;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load videos.\n\n{ex.Message}", "OK");
        }
    }

    private static string BuildVideoHtml(string url)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
<style>
html, body {{
    margin:0;
    padding:0;
    background:#000;
    width:100%;
    height:100%;
}}
video {{
    width:100%;
    height:100%;
    background:#000;
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

    public sealed class VideoDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    public sealed class VideoItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public HtmlWebViewSource? VideoHtml { get; set; }
    }
}