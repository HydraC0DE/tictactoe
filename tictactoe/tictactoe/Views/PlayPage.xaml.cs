namespace tictactoe.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using System.Text.Json;
using System.Xml.Linq;
using tictactoe.Data;
using tictactoe.Models;
using tictactoe.Services;

[QueryProperty(nameof(GameJson), "game")]
[QueryProperty(nameof(Row), "row")]
[QueryProperty(nameof(Col), "col")]
[QueryProperty(nameof(PhotoPath), "photoPath")]
public partial class PlayPage : ContentPage
{
    //private FileResult Picture;
    public string PhotoPath { get; set; }
    private readonly MatchRepository _repo;
    private Game _game;
    private ITicTacToeSolver _solver;
    public string BestMove { get; set; }


    public int Row { get; set; }
    public int Col { get; set; }

    private Button[,] _boardButtons = new Button[Game.SIZE, Game.SIZE];
    public string GameJson
    {
        set
        {
            if (string.IsNullOrEmpty(value))
                return;


            string unescaped = Uri.UnescapeDataString(value);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            _game = JsonSerializer.Deserialize<Game>(unescaped, options);


            if (_game != null)
            {

                lblBestMove.Text = $"Suggested move: ({Row}, {Col})";

                //UglyHelper();
                InitializeBoardGrid();
            }
        }
    }


    public PlayPage(MatchRepository repo, ITicTacToeSolver solver)
    {
        InitializeComponent();
        _repo = repo;
        _solver = solver;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        btnAIMove.IsEnabled = true;

    }



    private async Task SaveMatchAsync()
    {
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync();
            double latitude = location?.Latitude ?? 0;
            double longitude = location?.Longitude ?? 0;


            var match = new Match
            {
                Date = DateTime.Now,
                ImagePath = PhotoPath,
                Latitude = latitude,
                Longitude = longitude,
                Result = _game.Result,
                CurrentGame = _game,
            };

            await _repo.AddMatchAsync(match);
            await DisplayAlert("Saved", "Match and photo saved with location", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save match: {ex.Message}", "OK");
        }
    }
    private async void OnSaveMatchClicked(object sender, EventArgs e)
    {
        await SaveMatchAsync();
    }




    private void InitializeBoardGrid()
    {
        BoardGrid.Children.Clear();
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();

        for (int i = 0; i < Game.SIZE; i++)
        {
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (int r = 0; r < Game.SIZE; r++)
        {
            for (int c = 0; c < Game.SIZE; c++)
            {
                var btn = new Button
                {
                    WidthRequest = 10,
                    HeightRequest = 10,
                    FontSize = 20,
                    Padding = 1,
                    Margin = 1,
                    BackgroundColor = Colors.LightGray,
                    Text = _game.Board[r, c] == 1 ? "X" :
                           _game.Board[r, c] == 2 ? "O" : "",
                    IsEnabled = false
                };

                int row = r;
                int col = c;
                btn.Clicked += (s, e) => OnCellClicked(row, col);

                _boardButtons[r, c] = btn;
                BoardGrid.Children.Add(btn);
                Grid.SetRow(btn, r);
                Grid.SetColumn(btn, c);
            }

        }
    }
    bool CanPlayerMove(string player) => _game.NextMove == player;


    private async void UpdateCell(int row, int col)
    {

        var btn = _boardButtons[row, col];
        btn.Text = _game.Board[row, col] == 1 ? "X" :
                   _game.Board[row, col] == 2 ? "O" : "";

        if (_game.IsTerminal)
        {

            btnAIMove.IsEnabled = false;
            // Disable the entire board
            foreach (var b in _boardButtons)
                b.IsEnabled = false;

            // Show result
            string winner = _game.Result; // "X", "O", or "Draw"
            string message = winner == "Draw"
                ? "The game ended in a draw. Do you want to save the match?"
                : $"{winner} won! Do you want to save the match?";

            bool save = await DisplayAlert("Game Over", message, "Yes", "No");

            if (save)
            {
                await SaveMatchAsync();
            }
        }

    }
    private async void EnableCorrectOnes()
    {
        //bool isPlayerTurn = _game.NextMove == "X"; // human is X
        bool isPlayerTurn = true;

        for (int r = 0; r < Game.SIZE; r++)
        {
            for (int c = 0; c < Game.SIZE; c++)
            {
                var btn = _boardButtons[r, c];

                // Disable filled cells or game over
                if (_game.Board[r, c] != 0 || _game.IsTerminal)
                {
                    btn.IsEnabled = false;
                    continue;
                }

                // Player's turn: enable only adjacent cells (or center if first move)
                if (isPlayerTurn)
                {
                    if (!_game.HasAnyPiece())
                        btn.IsEnabled = r == Game.SIZE / 2 && c == Game.SIZE / 2;
                    else
                        btn.IsEnabled = _game.IsAdjacent(r, c);
                }
                else
                {
                    // AI turn: disable all buttons before it moves
                    btn.IsEnabled = false;
                }
            }
        }
    }

    private void OnCellClicked(int row, int col)
    {
        // ensure it's player's turn and spot is legal
        if (_game.NextMove != "X") return;
        if (!_game.InBounds(row, col)) return;
        if (_game.Board[row, col] != 0) return;
        if (_game.HasAnyPiece() && !_game.IsAdjacent(row, col)) return;

        bool ok = _game.MakeMove(row, col);
        if (ok)
        {
            UpdateCell(row, col);
            btnAIMove.IsEnabled = true;
            // disable UI while AI thinks, etc...
        }
    }


    private async void OnGetAIMoveClicked(object sender, EventArgs e)
    {
        btnAIMove.IsEnabled = false;
        var aiMove = await _solver.GetBestMoveAsync(_game);
        if (aiMove.row >= 0)
        {
            _game.MakeMove(aiMove.row, aiMove.col);
            UpdateCell(aiMove.row, aiMove.col);
        }
        EnableCorrectOnes();
    }


}