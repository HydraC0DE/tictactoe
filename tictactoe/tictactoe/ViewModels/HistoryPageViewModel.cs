using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;
using tictactoe.ViewModels;
using tictactoe.Views;

namespace tictactoe.ViewModels;

public partial class HistoryPageViewModel : ObservableObject
{
    private readonly MatchRepository _repo;

    [ObservableProperty]
    private ObservableCollection<Match> matches = new();

    public HistoryPageViewModel(MatchRepository repo)
    {
        _repo = repo;
    }

    public async Task LoadAsync()
    {
        var all = await _repo.GetAllMatchesAsync();
        Matches = new ObservableCollection<Match>(all);
    }

    [RelayCommand]
    private async Task MatchSelected(Match match)
    {
        if (match == null)
            return;

        var json = JsonSerializer.Serialize(match);
        var escaped = Uri.EscapeDataString(json);

        await Shell.Current.GoToAsync($"{nameof(MatchDetailPage)}?match={escaped}");
    }

    [ObservableProperty]
    private Match selectedMatch;

    partial void OnSelectedMatchChanged(Match value)
    {
        if (value == null) return;
        MatchSelectedCommand.Execute(value);
        SelectedMatch = null;
    }

}
