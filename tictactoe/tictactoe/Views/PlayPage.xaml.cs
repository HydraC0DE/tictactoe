namespace tictactoe.Views;

[QueryProperty(nameof(BestMove), "bestMove")]
public partial class PlayPage : ContentPage
{
    public string BestMove { get; set; }

    public PlayPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(BestMove))
        {
            DisplayAlert("AI Move", $"Solver suggests: {BestMove}", "OK");
            // TODO: Fill the board UI based on this string
        }
    }
}


