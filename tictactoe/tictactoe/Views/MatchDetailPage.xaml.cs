//using System.Globalization;
//using System.Text.Json;
//using System.Text.RegularExpressions;
//using tictactoe.Data;
//using tictactoe.Models;

//namespace tictactoe.Views;

//[QueryProperty(nameof(MatchJson), "match")]

//public partial class MatchDetailPage : ContentPage
//{
//    private Models.Match _match;
//    public int MatchId { get; set; }

//    private readonly MatchRepository _repo;
//    public string MatchJson
//    {
//        set
//        {
//            if (string.IsNullOrEmpty(value)) return;

//            // Deserialize JSON string into Match object
//            _match = JsonSerializer.Deserialize<Models.Match>(Uri.UnescapeDataString(value));

//            // Generate map HTML immediately
//            GenerateMapHtml();
//            BindingContext = _match;
//            ShowImage();
//        }
//    }

//    public MatchDetailPage(MatchRepository repo) //it doesnt get the match directly so i can refresh the map 
//    {
//        InitializeComponent();
//        _repo = repo;
//    }

//    private void ShowImage()
//    {
//        if (_match != null && !string.IsNullOrEmpty(_match.ImagePath) && File.Exists(_match.ImagePath))
//        {
//            imgMatch.Source = ImageSource.FromFile(_match.ImagePath);
//        }
//        else
//        {
//            imgMatch.Source = null; // or a placeholder
//        }
//    }
//    private void GenerateMapHtml()
//    {
//        if (_match == null)
//            return;

//        // Convert latitude and longitude to invariant culture strings (dot decimal separator)
//        string lat = _match.Latitude.ToString(CultureInfo.InvariantCulture);
//        string lon = _match.Longitude.ToString(CultureInfo.InvariantCulture);

//        string html = $@"
//<!DOCTYPE html>
//<html>
//<head>
//<meta name='viewport' content='width=device-width, initial-scale=1.0'>
//<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
//<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
//<style>
//    #map {{ height: 100%; width: 100%; }}
//    html, body {{ height: 100%; margin: 0; padding: 0; }}
//</style>
//</head>
//<body>
//<div id='map'></div>
//<script>
//    // Initialize the map at the match location
//    var map = L.map('map').setView([{lat}, {lon}], 13);

//    // Add OpenStreetMap tiles
//    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
//        maxZoom: 19,
//        attribution: '© OpenStreetMap contributors'
//    }}).addTo(map);

//    // Add a marker for the match
//    L.marker([{lat}, {lon}])
//        .addTo(map)
//        .bindPopup('<b>{_match.CurrentGame.Result}</b><br>{_match.Date.ToString()}');
//</script>
//</body>
//</html>";

//        MapWebView.Source = new HtmlWebViewSource { Html = html };
//    }

//    //private async void OnOpenImageClicked(object sender, EventArgs e)
//    //{
//    //    if (_match != null && !string.IsNullOrEmpty(_match.ImagePath) && File.Exists(_match.ImagePath))
//    //    {
//    //        // Optionally open with platform gallery or display full-screen
//    //        await Navigation.PushAsync(new ImageFullScreenPage(_match.ImagePath));
//    //    }
//    //    else
//    //    {
//    //        await DisplayAlert("No image", "No saved image for this match.", "OK");
//    //    }
//    //}

//    // Optional: minimal CRUD handlers
//    private async void OnEditClicked(object sender, EventArgs e)
//    {
//        if (_match == null) return;

//        // Example: edit Result (you could show a popup to get new value)
//        string newResult = await DisplayPromptAsync("Edit Match", "Enter new result:", initialValue: _match.CurrentGame.Result);
//        if (!string.IsNullOrEmpty(newResult))
//        {
//            _match.CurrentGame.Result = newResult;
//            await _repo.UpdateMatchAsync(_match);
//            await DisplayAlert("Success", "Match updated!", "OK");

//            // Refresh the map popup text
//            GenerateMapHtml();
//        }
//    }

//    private async void OnDeleteClicked(object sender, EventArgs e)
//    {
//        if (_match == null) return;

//        bool confirm = await DisplayAlert("Confirm Delete",
//            $"Are you sure you want to delete match '{_match.CurrentGame.Result}'?",
//            "Yes", "No");

//        if (confirm)
//        {
//            await _repo.DeleteMatchAsync(_match);
//            await Shell.Current.GoToAsync(".."); // Navigate back to list
//        }
//    }
//}

//using System.Globalization;
//using System.Text.Json;
//using tictactoe.Data;
//using tictactoe.Models;
//using Microsoft.Maui.Controls;

//namespace tictactoe.Views;

//[QueryProperty(nameof(MatchJson), "match")]
//public partial class MatchDetailPage : ContentPage
//{
//    private Match _match;
//    private readonly MatchRepository _repo;
//    private Button[,] _boardButtons;

//    public string MatchJson
//    {
//        set
//        {
//            if (string.IsNullOrEmpty(value)) return;

//            _match = JsonSerializer.Deserialize<Match>(Uri.UnescapeDataString(value));

//            BindingContext = _match;
//            ShowImage();
//            GenerateMapHtml();
//            InitializeBoardGrid();
//        }
//    }

//    public MatchDetailPage(MatchRepository repo)
//    {
//        InitializeComponent();
//        _repo = repo;
//    }

//    private void ShowImage()
//    {
//        if (_match != null && !string.IsNullOrEmpty(_match.ImagePath) && File.Exists(_match.ImagePath))
//        {
//            imgMatch.Source = ImageSource.FromFile(_match.ImagePath);
//        }
//        else
//        {
//            imgMatch.Source = null;
//        }
//    }

//    private void GenerateMapHtml()
//    {
//        if (_match == null) return;

//        string lat = _match.Latitude.ToString(CultureInfo.InvariantCulture);
//        string lon = _match.Longitude.ToString(CultureInfo.InvariantCulture);

//        string html = $@"
//<!DOCTYPE html>
//<html>
//<head>
//<meta name='viewport' content='width=device-width, initial-scale=1.0'>
//<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
//<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
//<style>
//    #map {{ height: 100%; width: 100%; }}
//    html, body {{ height: 100%; margin: 0; padding: 0; }}
//</style>
//</head>
//<body>
//<div id='map'></div>
//<script>
//    var map = L.map('map').setView([{lat}, {lon}], 13);
//    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
//        maxZoom: 19,
//        attribution: '© OpenStreetMap contributors'
//    }}).addTo(map);
//    L.marker([{lat}, {lon}]).addTo(map)
//        .bindPopup('<b>{_match.CurrentGame.Result}</b><br>{_match.Date}');
//</script>
//</body>
//</html>";

//        MapWebView.Source = new HtmlWebViewSource { Html = html };
//    }

//    private void InitializeBoardGrid()
//    {
//        if (_match?.CurrentGame == null) return;

//        BoardGrid.Children.Clear();
//        BoardGrid.RowDefinitions.Clear();
//        BoardGrid.ColumnDefinitions.Clear();

//        int size = Game.SIZE;
//        _boardButtons = new Button[size, size];

//        for (int i = 0; i < size; i++)
//        {
//            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
//        }

//        for (int r = 0; r < size; r++)
//        {
//            for (int c = 0; c < size; c++)
//            {
//                var btn = new Button
//                {
//                    WidthRequest = 40,
//                    HeightRequest = 40,
//                    FontSize = 20,
//                    BackgroundColor = Colors.LightGray,
//                    Text = _match.CurrentGame.Board[r, c] == 1 ? "X" :
//                           _match.CurrentGame.Board[r, c] == 2 ? "O" : "",
//                    IsEnabled = false // read-only
//                };

//                _boardButtons[r, c] = btn;
//                BoardGrid.Children.Add(btn);
//                Grid.SetRow(btn, r);
//                Grid.SetColumn(btn, c);
//            }
//        }
//    }

//    // Edit/Delete handlers
//    private async void OnEditClicked(object sender, EventArgs e)
//    {
//        if (_match == null) return;

//        string newResult = await DisplayPromptAsync("Edit Match", "Enter new result:", initialValue: _match.CurrentGame.Result);
//        if (!string.IsNullOrEmpty(newResult))
//        {
//            _match.CurrentGame.Result = newResult;
//            await _repo.UpdateMatchAsync(_match);
//            await DisplayAlert("Success", "Match updated!", "OK");

//            GenerateMapHtml();
//        }
//    }

//    private async void OnDeleteClicked(object sender, EventArgs e)
//    {
//        if (_match == null) return;

//        bool confirm = await DisplayAlert("Confirm Delete",
//            $"Are you sure you want to delete match '{_match.CurrentGame.Result}'?",
//            "Yes", "No");

//        if (confirm)
//        {
//            await _repo.DeleteMatchAsync(_match);
//            await Shell.Current.GoToAsync("..");
//        }
//    }
//}

using System.Globalization;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;
using Microsoft.Maui.Controls;

namespace tictactoe.Views;

[QueryProperty(nameof(MatchJson), "match")]
public partial class MatchDetailPage : ContentPage
{
    private Match _match;
    private readonly MatchRepository _repo;
    private Button[,] _boardButtons;

    public string MatchJson
    {
        set
        {
            if (string.IsNullOrEmpty(value)) return;

            _match = JsonSerializer.Deserialize<Match>(Uri.UnescapeDataString(value));
            BindingContext = _match;
            ShowImage();
            GenerateMapHtml();
            InitializeBoardGrid();
        }
    }

    public MatchDetailPage(MatchRepository repo)
    {
        InitializeComponent();
        _repo = repo;
    }

    private void ShowImage()
    {
        if (_match != null && !string.IsNullOrEmpty(_match.ImagePath) && File.Exists(_match.ImagePath))
        {
            imgMatch.Source = ImageSource.FromFile(_match.ImagePath);
        }
        else
        {
            imgMatch.Source = null;
        }
    }

    private void GenerateMapHtml()
    {
        if (_match == null) return;

        string lat = _match.Latitude.ToString(CultureInfo.InvariantCulture);
        string lon = _match.Longitude.ToString(CultureInfo.InvariantCulture);

        string html = $@"
<!DOCTYPE html>
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
    #map {{ height: 100%; width: 100%; }}
    html, body {{ height: 100%; margin: 0; padding: 0; }}
</style>
</head>
<body>
<div id='map'></div>
<script>
    var map = L.map('map').setView([{lat}, {lon}], 13);
    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }}).addTo(map);
    L.marker([{lat}, {lon}]).addTo(map)
        .bindPopup('<b>{_match.CurrentGame.Result}</b><br>{_match.Date}');
</script>
</body>
</html>";

        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }

    //private void InitializeBoardGrid()
    //{
    //    if (_match?.CurrentGame == null) return;

    //    BoardGrid.Children.Clear();
    //    BoardGrid.RowDefinitions.Clear();
    //    BoardGrid.ColumnDefinitions.Clear();

    //    int size = Game.SIZE;
    //    _boardButtons = new Button[size, size];

    //    for (int i = 0; i < size; i++)
    //    {
    //        BoardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    //        BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    //    }

    //    for (int r = 0; r < size; r++)
    //    {
    //        for (int c = 0; c < size; c++)
    //        {
    //            var btn = new Button
    //            {
    //                WidthRequest = 10,
    //                HeightRequest = 10,
    //                FontSize = 20,
    //                Padding = 1,
    //                Margin = 1,
    //                BackgroundColor = Colors.LightGray,
    //                Text = _match.CurrentGame.Board[r, c] == 1 ? "X" :
    //                       _match.CurrentGame.Board[r, c] == 2 ? "O" : "",
    //                IsEnabled = false
    //            };

    //            _boardButtons[r, c] = btn;
    //            BoardGrid.Children.Add(btn);
    //            Grid.SetRow(btn, r);
    //            Grid.SetColumn(btn, c);
    //        }
    //    }
    //}

    private void InitializeBoardGrid()
    {
        if (_match?.CurrentGame == null) return;

        int size = Game.SIZE;
        _boardButtons = new Button[size, size];
        BoardGrid.Children.Clear();
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();

        // Fixed row/column sizes for uniform squares
        for (int i = 0; i < size; i++)
        {
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = 10 }); // adjust as needed
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = 10 });
        }

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                var btn = new Button
                {
                    WidthRequest = 10,
                    HeightRequest = 10,
                    FontSize = 20,
                    Padding = 1,
                    Margin = 1,
                    BackgroundColor = Colors.LightGray,
                    Text = _match.CurrentGame.Board[r, c] == 1 ? "X" :
                           _match.CurrentGame.Board[r, c] == 2 ? "O" : "",
                    IsEnabled = false
                };

                _boardButtons[r, c] = btn;
                BoardGrid.Children.Add(btn);
                Grid.SetRow(btn, r);
                Grid.SetColumn(btn, c);
            }
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_match == null) return;

        string newResult = await DisplayPromptAsync("Edit Match", "Enter new result:", initialValue: _match.CurrentGame.Result);
        if (!string.IsNullOrEmpty(newResult))
        {
            _match.CurrentGame.Result = newResult;
            await _repo.UpdateMatchAsync(_match);
            await DisplayAlert("Success", "Match updated!", "OK");
            GenerateMapHtml();
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_match == null) return;

        bool confirm = await DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete match '{_match.CurrentGame.Result}'?",
            "Yes", "No");

        if (confirm)
        {
            await _repo.DeleteMatchAsync(_match);
            await Shell.Current.GoToAsync("..");
        }
    }
}
