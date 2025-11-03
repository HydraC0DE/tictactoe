using Microsoft.Maui.Controls;
using System.IO;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;
using tictactoe.Services;

namespace tictactoe.Views;

public partial class PicturePage : ContentPage
{
    private string _photoPathPersistent;
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

            string cachePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
            using (Stream sourceStream = await photo.OpenReadAsync())
            using (FileStream cacheStream = File.OpenWrite(cachePath))
            {
                await sourceStream.CopyToAsync(cacheStream);
            }




            string ext = Path.GetExtension(photo.FileName);
            string destFileName = $"{Guid.NewGuid()}{ext}";
            string destPath = Path.Combine(FileSystem.AppDataDirectory, destFileName);

            File.Copy(cachePath, destPath, overwrite: false);

            _photoPathPersistent = destPath;

            imgPreview.Source = ImageSource.FromFile(cachePath);

            _detectedGame = await _solver.ProcessImageAsync(cachePath);
            
            
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
            //string gameJson = JsonSerializer.Serialize(_detectedGame);

            //await Shell.Current.GoToAsync(
            //    $"///play?game={Uri.EscapeDataString(gameJson)}&row={_suggestedMove.row}&col={_suggestedMove.col}"
            //);

            string gameJson = JsonSerializer.Serialize(_detectedGame);
            await Shell.Current.GoToAsync(
                $"///play?game={Uri.EscapeDataString(gameJson)}" +
                $"&row={_suggestedMove.row}&col={_suggestedMove.col}" +
                $"&photoPath={Uri.EscapeDataString(_photoPathPersistent)}"
            );

        }
    }

}
