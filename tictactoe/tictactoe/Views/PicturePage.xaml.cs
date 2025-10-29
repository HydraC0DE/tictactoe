using Microsoft.Maui.Controls;
using System.IO;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;
using tictactoe.Services;

namespace tictactoe.Views;

public partial class PicturePage : ContentPage
{
    private readonly ITicTacToeSolver _solver;
    private string _solverResult;
    private Game _detectedGame;
    private (int row, int col) _suggestedMove;

    public PicturePage(ITicTacToeSolver solver)
    {
        InitializeComponent();
        _solver = solver;
    }

    private async Task TakePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlert("Error", "Camera not supported on this device.", "OK");
            return;
        }

        try
        {
            //FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            //if (photo == null)
            //    return; // user canceled

            //// Save photo locally
            //string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
            //using Stream sourceStream = await photo.OpenReadAsync();
            //using FileStream localFileStream = File.OpenWrite(localFilePath);
            //await sourceStream.CopyToAsync(localFileStream);

            //// Show photo preview
            //imgPreview.Source = ImageSource.FromFile(localFilePath);

            //// Call solver and store result
            //_solverResult = await _solver.GetBestMoveAsync(localFilePath);

            //// Show the Go to Play button
            //btnGoToPlay.IsVisible = true;

            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo == null) return;

            string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
            using Stream sourceStream = await photo.OpenReadAsync();
            using FileStream localFileStream = File.OpenWrite(localFilePath);
            await sourceStream.CopyToAsync(localFileStream);
            imgPreview.Source = ImageSource.FromFile(localFilePath);

            // Step 1: process image → game state
            _detectedGame = await _solver.ProcessImageAsync(localFilePath);

            // Step 2: get best move suggestion
            _suggestedMove = await _solver.GetBestMoveAsync(_detectedGame);

            btnGoToPlay.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnCameraClicked(object sender, EventArgs e)
    {
        await TakePhotoAsync();
    }

    private async void OnGoToPlayClicked(object sender, EventArgs e)
    {
        if (_detectedGame != null)
        {
            string gameJson = JsonSerializer.Serialize(_detectedGame);
            await Shell.Current.GoToAsync(
                $"///play?game={Uri.EscapeDataString(gameJson)}&row={_suggestedMove.row}&col={_suggestedMove.col}"
            );
        }
    }

}
