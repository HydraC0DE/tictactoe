
namespace tictactoe.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using System.Text.Json;
using System.Xml.Linq;
using tictactoe.Data;
using tictactoe.Models;

[QueryProperty(nameof(GameJson), "game")]
[QueryProperty(nameof(Row), "row")]
[QueryProperty(nameof(Col), "col")]
[QueryProperty(nameof(PhotoPath), "photoPath")]
public partial class PlayPage : ContentPage
{
    private readonly MatchRepository _repo;
    private Game _game;
    public string BestMove { get; set; }
    public string PhotoPath { get; set; }

    public int Row { get; set; }
    public int Col { get; set; }

    public string GameJson
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                _game = JsonSerializer.Deserialize<Game>(Uri.UnescapeDataString(value));
        }
    }

    public PlayPage(MatchRepository repo)
    {
        InitializeComponent();
        _repo = repo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_game != null)
        {
            lblBestMove.Text = $"Suggested move: ({Row}, {Col})";
        }
    }

    private async void OnSaveMatchClicked(object sender, EventArgs e)
    {
        try
        {
            // Get current location
            var location = await Geolocation.GetLastKnownLocationAsync();
            double latitude = 0;
            double longitude = 0;

            if (location != null)
            {
                latitude = location.Latitude;
                longitude = location.Longitude;
            }

            // TODO: replace 'true' with proper completion check
            if (true)
            {
                var match = new Match
                {
                    //Result = "X won", // placeholder
                    Date = DateTime.Now,
                    ImagePath = PhotoPath,
                    Latitude = latitude,
                    Longitude = longitude
                };

                await _repo.AddMatchAsync(match);
                await DisplayAlert("Saved", "Match and photo saved with location", "OK");
            }
            
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save match: {ex.Message}", "OK");
        }
    }
}
