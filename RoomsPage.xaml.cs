using System.Collections.ObjectModel;
using System.Text.Json;

namespace PROXIMAMOP.Pages;

public partial class RoomsPage : ContentPage
{
    private const string RoomApi = "http://193.34.213.124:7070";
    private readonly HttpClient _http = new();
    private readonly ObservableCollection<RoomItem> _rooms = [];

    public RoomsPage()
    {
        InitializeComponent();
        RoomsList.ItemsSource = _rooms;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRoomsAsync();
    }

    private async Task LoadRoomsAsync()
    {
        _rooms.Clear();

        var json = await _http.GetStringAsync($"{RoomApi}/api/rooms");
        var rooms = JsonSerializer.Deserialize<List<RoomItem>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        foreach (var room in rooms)
            _rooms.Add(room);
    }

    private async void OnRoomTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border b || b.BindingContext is not RoomItem room)
            return;

        string password = await DisplayPromptAsync(
            "Room Password",
            $"Enter password for {room.Name}",
            "Enter",
            "Cancel",
            "Password",
            maxLength: 50,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(password))
            return;

        await Navigation.PushAsync(new RoomVideosPage(room.Id, room.Name, password));
    }

    public sealed class RoomItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }
}