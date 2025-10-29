using System.Text.Json;
using tictactoe.Models;
using System.Globalization;
using tictactoe.Data;

namespace tictactoe.Views;

[QueryProperty(nameof(MatchJson), "match")]
public partial class MatchDetailPage : ContentPage
{
    private Match _match;
    private readonly MatchRepository _repo;
    public string MatchJson
    {
        set
        {
            if (string.IsNullOrEmpty(value)) return;

            // Deserialize JSON string into Match object
            _match = JsonSerializer.Deserialize<Match>(Uri.UnescapeDataString(value));

            // Generate map HTML immediately
            GenerateMapHtml();
        }
    }

    public MatchDetailPage(MatchRepository repo) //it doesnt get the match directly so i can refresh the map 
    {
        InitializeComponent();
        _repo = repo;
    }

    private void GenerateMapHtml()
    {
        if (_match == null)
            return;

        // Convert latitude and longitude to invariant culture strings (dot decimal separator)
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
    // Initialize the map at the match location
    var map = L.map('map').setView([{lat}, {lon}], 13);

    // Add OpenStreetMap tiles
    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }}).addTo(map);

    // Add a marker for the match
    L.marker([{lat}, {lon}])
        .addTo(map)
        .bindPopup('<b>{_match.Result}</b><br>{_match.Date.ToString()}');
</script>
</body>
</html>";
        
        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }

    // Optional: minimal CRUD handlers
    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_match == null) return;

        // Example: edit Result (you could show a popup to get new value)
        string newResult = await DisplayPromptAsync("Edit Match", "Enter new result:", initialValue: _match.Result);
        if (!string.IsNullOrEmpty(newResult))
        {
            _match.Result = newResult;
            await _repo.UpdateMatchAsync(_match);
            await DisplayAlert("Success", "Match updated!", "OK");

            // Refresh the map popup text
            GenerateMapHtml();
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_match == null) return;

        bool confirm = await DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete match '{_match.Result}'?",
            "Yes", "No");

        if (confirm)
        {
            await _repo.DeleteMatchAsync(_match);
            await Shell.Current.GoToAsync(".."); // Navigate back to list
        }
    }
}
