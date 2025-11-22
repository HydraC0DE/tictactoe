using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;

namespace tictactoe.ViewModels;

public partial class MatchDetailPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly MatchRepository _repo;

    [ObservableProperty]
    private string resultDisplay;

    [ObservableProperty]
    private Match match;

    [ObservableProperty]
    private string imagePath;

    [ObservableProperty]
    private string mapHtml;

    [ObservableProperty]
    private string emojiBoard;

    public MatchDetailPageViewModel(MatchRepository repo)
    {
        _repo = repo;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.ContainsKey("match"))
            return;

        string json = Uri.UnescapeDataString(query["match"].ToString());
        Match = JsonSerializer.Deserialize<Match>(json);

        ImagePath = GetImageSource();
        MapHtml = BuildMapHtml();
        EmojiBoard = BuildShareBoardText(Match.CurrentGame.Board);

        RefreshResultDisplay();
    }

    private string GetImageSource()
    {
        if (!string.IsNullOrEmpty(Match?.ImagePath) &&
            File.Exists(Match.ImagePath))
            return Match.ImagePath;

        return null;
    }

    private string BuildMapHtml()
    {
        if (Match == null) return "";

        string lat = Match.Latitude.ToString(CultureInfo.InvariantCulture);
        string lon = Match.Longitude.ToString(CultureInfo.InvariantCulture);

        return $@"
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
        .bindPopup('<b>{Match.CurrentGame.Result}</b><br>{Match.Date}');
</script>
</body>
</html>";
    }

    private void RefreshResultDisplay()
    {
        ResultDisplay = Match?.CurrentGame?.Result switch
        {
            null => "",
            "Draw" => "Draw",
            var r => $"{r} has won the match!"
        };
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (Match == null)
        {
            ;
            return;
        }
        // Prompt user for new result
        string newResult = await Application.Current.MainPage.DisplayPromptAsync(
            "Edit Match",
            "Enter new result:");

        if (string.IsNullOrEmpty(newResult))
            return;

        // Update both CurrentGame and the database column
        Match.CurrentGame.Result = newResult;
        Match.Result = newResult; // <-- critical for SQLite

        // Persist to database
        await _repo.UpdateMatchAsync(Match);

        // Refresh UI-bound properties
        RefreshResultDisplay();
        MapHtml = BuildMapHtml();
    }




    partial void OnMatchChanged(Match value)
    {
        if (value != null)
        {
            // always keep Result in sync with CurrentGame.Result
            ResultDisplay = value.CurrentGame.Result;
        }
    }


    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Match == null) return;

        bool ok = await Application.Current!.MainPage!.DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete match '{Match.CurrentGame.Result}'?",
            "Yes", "No");

        if (!ok) return;

        await _repo.DeleteMatchAsync(Match);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        string winner = Match.CurrentGame.Result;
        string boardText = EmojiBoard;
        string fakeUrl = "https://very-real-tictactoe-ai.playstore.com";

        string text =
            $"Hey look at this game I just played!\n" +
            $"The winner was {winner}.\n\n" +
            $"Board:\n{boardText}\n\n" +
            $"Download here: {fakeUrl}";

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = text,
            Title = "Share Match"
        });
    }

    private string BuildShareBoardText(int[,] board)
    {
        int size = Game.SIZE;

        int minR = size, maxR = -1, minC = size, maxC = -1;

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (board[r, c] != 0)
                {
                    minR = Math.Min(minR, r);
                    maxR = Math.Max(maxR, r);
                    minC = Math.Min(minC, c);
                    maxC = Math.Max(maxC, c);
                }
            }
        }

        if (maxR == -1)
        {
            minR = minC = (size / 2) - 2;
            maxR = maxC = (size / 2) + 2;
        }

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

        minR = Math.Max(0, minR);
        minC = Math.Max(0, minC);
        maxR = Math.Min(size - 1, maxR);
        maxC = Math.Min(size - 1, maxC);

        string Row(int r)
        {
            string s = "";
            for (int c = minC; c <= maxC; c++)
                s += board[r, c] == 1 ? "❌" :
                     board[r, c] == 2 ? "⭕" : "▫️";
            return s;
        }

        var lines = new List<string>();
        for (int r = minR; r <= maxR; r++)
            lines.Add(Row(r));

        return string.Join("\n", lines);
    }
}
