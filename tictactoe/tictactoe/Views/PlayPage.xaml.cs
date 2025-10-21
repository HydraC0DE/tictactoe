namespace tictactoe.Views;
using tictactoe.Data;
using tictactoe.Models;

[QueryProperty(nameof(BestMove), "bestMove")]
public partial class PlayPage : ContentPage
{
    private readonly MatchRepository _repo;


    public string BestMove { get; set; }

    public PlayPage(MatchRepository repo)
    {
        InitializeComponent();
        _repo = repo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Only show alert if BestMove has a value
        if (!string.IsNullOrEmpty(BestMove))
        {
            DisplayAlert("AI Move", $"Solver suggests: {BestMove}", "OK");
        }

     
    }

    private async void OnSaveMatchClicked(object sender, EventArgs e)
    {
        // TODO: replace 'true' with proper completion check
        if (true)
        {
            var match = new Match
            {
                Result = "X won", // placeholder
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await _repo.AddMatchAsync(match);
            await DisplayAlert("Saved", "Match saved to history", "OK");
        }
    }
}
