using System.IO;
using Microsoft.Maui.Controls;
using tictactoe.Data;
using tictactoe.Services;

namespace tictactoe.Views;

public partial class PicturePage : ContentPage
{
    private readonly ITicTacToeSolver _solver;
    private string _solverResult;

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
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo == null)
                return; // user canceled

            // Save photo locally
            string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
            using Stream sourceStream = await photo.OpenReadAsync();
            using FileStream localFileStream = File.OpenWrite(localFilePath);
            await sourceStream.CopyToAsync(localFileStream);

            // Show photo preview
            imgPreview.Source = ImageSource.FromFile(localFilePath);

            // Call solver and store result
            _solverResult = await _solver.GetBestMoveAsync(localFilePath);

            // Show the Go to Play button
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
        if (!string.IsNullOrEmpty(_solverResult))
        {
            // Navigate to PlayPage with query parameter
            await Shell.Current.GoToAsync($"///play?bestMove={Uri.EscapeDataString(_solverResult)}");
        }
    }
}
