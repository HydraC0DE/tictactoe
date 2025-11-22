using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;
using tictactoe.Services;

namespace tictactoe.ViewModels;

public partial class PlayPageViewModel : ObservableObject
{
    private readonly ITicTacToeSolver _solver;
    private readonly MatchRepository _repo;

    private Grid _boardGrid;
    private Button[,] _buttons;

    public string PhotoPath { get; set; }

    private Game _game;

    [ObservableProperty]
    private string bestMoveText = "Waiting for image analysis...";

    private int suggestedRow;
    private int suggestedCol;

    public PlayPageViewModel(ITicTacToeSolver solver, MatchRepository repo)
    {
        _solver = solver;
        _repo = repo;
    }

    public void AttachBoard(Grid grid)
    {
        _boardGrid = grid;
    }

    public void LoadGame(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        string unescaped = Uri.UnescapeDataString(json);

        _game = JsonSerializer.Deserialize<Game>(unescaped, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        BuildBoard();
        UpdateBestMoveText();
    }

    public void SetSuggestedMove(int row, int col)
    {
        suggestedRow = row;
        suggestedCol = col;
        UpdateBestMoveText();
    }

    private void UpdateBestMoveText()
    {
        BestMoveText = $"Suggested move: ({suggestedRow}, {suggestedCol})";
    }

    private void BuildBoard()
    {
        if (_boardGrid == null || _game == null)
            return;

        _boardGrid.Children.Clear();
        _boardGrid.RowDefinitions.Clear();
        _boardGrid.ColumnDefinitions.Clear();

        int size = Game.SIZE;

        _buttons = new Button[size, size];

        for (int i = 0; i < size; i++)
        {
            _boardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _boardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                var btn = new Button
                {
                    WidthRequest = 45,
                    HeightRequest = 45,
                    FontSize = 24,
                    Text = _game.Board[r, c] == 1 ? "X" :
                           _game.Board[r, c] == 2 ? "O" : "",
                    IsEnabled = false,
                    BackgroundColor = Colors.LightGray,
                    Margin = 1
                };

                int rr = r;
                int cc = c;
                btn.Clicked += (s, e) => PlayerClick(rr, cc);

                _buttons[r, c] = btn;
                _boardGrid.Children.Add(btn);
                Grid.SetRow(btn, r);
                Grid.SetColumn(btn, c);
            }
        }
    }

    private void PlayerClick(int row, int col)
    {
        if (_game.NextMove != "X") return;
        if (!_game.InBounds(row, col)) return;
        if (_game.Board[row, col] != 0) return;
        if (_game.HasAnyPiece() && !_game.IsAdjacent(row, col)) return;

        if (_game.MakeMove(row, col))
        {
            UpdateCell(row, col);
        }
    }

    private void UpdateCell(int row, int col)
    {
        _buttons[row, col].Text = _game.Board[row, col] == 1 ? "X" :
                                  _game.Board[row, col] == 2 ? "O" : "";

        if (_game.IsTerminal)
        {
            DisableBoard();

            string winner = _game.Result;
            string msg = winner == "Draw"
                ? "Draw. Save match?"
                : $"{winner} won. Save match?";

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool save = await Shell.Current.DisplayAlert("Game Over", msg, "Yes", "No");
                if (save)
                    await SaveMatchAsync();
            });
        }
    }

    private void DisableBoard()
    {
        foreach (var btn in _buttons)
            btn.IsEnabled = false;
    }

    [RelayCommand]
    private async Task GetAIMoveAsync()
    {
        if (_game == null) return;

        var move = await _solver.GetBestMoveAsync(_game);

        if (move.row >= 0)
        {
            _game.MakeMove(move.row, move.col);
            UpdateCell(move.row, move.col);
        }
    }

    [RelayCommand]
    private async Task SaveMatchAsync()
    {
        try
        {
            var loc = await Geolocation.GetLastKnownLocationAsync();

            var match = new Match
            {
                Date = DateTime.Now,
                ImagePath = PhotoPath,
                Latitude = loc?.Latitude ?? 0,
                Longitude = loc?.Longitude ?? 0,
                Result = _game.Result,
                CurrentGame = _game
            };

            await _repo.AddMatchAsync(match);

            await Shell.Current.DisplayAlert("Saved", "Match stored with location.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
