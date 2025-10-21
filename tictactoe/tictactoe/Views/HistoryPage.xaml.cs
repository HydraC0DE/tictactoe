namespace tictactoe.Views;
using tictactoe.Data;

public partial class HistoryPage : ContentPage
{
    private readonly MatchRepository _repo;

    public HistoryPage(MatchRepository repo)
    {
        InitializeComponent();
        _repo = repo;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var matches = await _repo.GetAllMatchesAsync();
        MatchesList.ItemsSource = matches;
    }
}
