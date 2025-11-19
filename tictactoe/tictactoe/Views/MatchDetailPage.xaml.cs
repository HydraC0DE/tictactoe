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

    //    int size = Game.SIZE;
    //    _boardButtons = new Button[size, size];
    //    BoardGrid.Children.Clear();
    //    BoardGrid.RowDefinitions.Clear();
    //    BoardGrid.ColumnDefinitions.Clear();

    //    // Fixed row/column sizes for uniform squares
    //    for (int i = 0; i < size; i++)
    //    {
    //        BoardGrid.RowDefinitions.Add(new RowDefinition { Height = 10 }); // adjust as needed
    //        BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = 10 });
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

        string emojiBoard = BuildShareBoardText(_match.CurrentGame.Board);

        EmojiBoardLabel.Text = emojiBoard;
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

    private async void OnShareClicked(object sender, EventArgs e)
    {
        if (_match == null) return;

        string winner = _match.CurrentGame.Result;
        string boardText = BuildShareBoardText(_match.CurrentGame.Board);

        string fakeUrl = "https://very-real-tictactoe-ai.playstore.com";

        string shareText =
            $"Hey look at this game I just played!\n" +
            $"The winner was {winner}.\n\n" +
            $"Here was the board:\n{boardText}\n\n" +
            $"Download the game here: {fakeUrl}";

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = shareText,
            Title = "Share Match"
        });
    }


    private string BuildShareBoardText(int[,] board)
    {
        int size = Game.SIZE;

        int minR = size, maxR = -1, minC = size, maxC = -1;

        // Find bounding box of played area
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (board[r, c] != 0)
                {
                    if (r < minR) minR = r;
                    if (r > maxR) maxR = r;
                    if (c < minC) minC = c;
                    if (c > maxC) maxC = c;
                }
            }
        }

        // If no moves, fallback to 5x5 center
        if (maxR == -1)
        {
            minR = minC = (size / 2) - 2;
            maxR = maxC = (size / 2) + 2;
        }

        // Enforce minimum 5x5
        if (maxR - minR + 1 < 5)
        {
            int center = (minR + maxR) / 2;
            minR = center - 2;
            maxR = center + 2;
        }

        if (maxC - minC + 1 < 5)
        {
            int center = (minC + maxC) / 2;
            minC = center - 2;
            maxC = center + 2;
        }

        // Clamp boundaries to board limits
        minR = Math.Max(0, minR);
        minC = Math.Max(0, minC);
        maxR = Math.Min(size - 1, maxR);
        maxC = Math.Min(size - 1, maxC);

        // Emoji output
        string Row(int r)
        {
            string s = "";
            for (int c = minC; c <= maxC; c++)
            {
                s += board[r, c] == 1 ? "❌" :
                     board[r, c] == 2 ? "⭕" : "▫️";
            }
            return s;
        }

        var lines = new List<string>();
        for (int r = minR; r <= maxR; r++)
            lines.Add(Row(r));

        return string.Join("\n", lines);
    }


}
