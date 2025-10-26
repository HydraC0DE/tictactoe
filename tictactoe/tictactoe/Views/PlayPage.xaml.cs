//namespace tictactoe.Views;
//using tictactoe.Data;
//using tictactoe.Models;

//[QueryProperty(nameof(BestMove), "bestMove")]
//public partial class PlayPage : ContentPage
//{
//    private readonly MatchRepository _repo;


//    public string BestMove { get; set; }

//    public PlayPage(MatchRepository repo)
//    {
//        InitializeComponent();
//        _repo = repo;
//    }

//    protected override void OnAppearing()
//    {
//        base.OnAppearing();

//        // Only show alert if BestMove has a value
//        if (!string.IsNullOrEmpty(BestMove))
//        {
//            DisplayAlert("AI Move", $"Solver suggests: {BestMove}", "OK");
//        }


//    }

//    private async void OnSaveMatchClicked(object sender, EventArgs e)
//    {
//        // TODO: replace 'true' with proper completion check
//        if (true)
//        {
//            var match = new Match
//            {
//                Result = "X won", // placeholder
//                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
//            };

//            await _repo.AddMatchAsync(match);
//            await DisplayAlert("Saved", "Match saved to history", "OK");
//        }
//    }
//}

namespace tictactoe.Views;
using tictactoe.Data;
using tictactoe.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

[QueryProperty(nameof(BestMove), "bestMove")]
[QueryProperty(nameof(PhotoPath), "photoPath")]
public partial class PlayPage : ContentPage
{
    private readonly MatchRepository _repo;

    public string BestMove { get; set; }
    public string PhotoPath { get; set; }

    public PlayPage(MatchRepository repo)
    {
        InitializeComponent();
        _repo = repo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(BestMove))
        {
            DisplayAlert("AI Move", $"Solver suggests: {BestMove}", "OK");
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
                    Result = "X won", // placeholder
                    Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
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
