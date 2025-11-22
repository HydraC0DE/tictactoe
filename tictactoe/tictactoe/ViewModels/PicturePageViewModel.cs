using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using System.IO;
using System.Text.Json;
using tictactoe.Data;
using tictactoe.Models;
using tictactoe.Services;

namespace tictactoe.ViewModels
{
    public partial class PicturePageViewModel : ObservableObject
    {
        private readonly ITicTacToeSolver _solver;
        private readonly IImageProcessor _imageProcessor;

        private string _photoPathPersistent;

        [ObservableProperty]
        private bool showGoButton;

        [ObservableProperty]
        private ImageSource previewImage;

        [ObservableProperty]
        private bool isProcessing;

        private Game _detectedGame;
        private (int row, int col) _suggestedMove;

        public PicturePageViewModel(ITicTacToeSolver solver, IImageProcessor processor)
        {
            _solver = solver;
            _imageProcessor = processor;

            ShowRules();
        }

        private async void ShowRules()
        {
            WeakReferenceMessenger.Default.Send(
                "Try to get a photo that is not too skewed, rotation does not matter.\n" +
                "Try to not use the flashlight unless necessary.\n" +
                "The player with X made the first move and the picture is taken on the turn of O having to make their move.\n" +
                "The AI will tell the best move for O.");
        }
        [RelayCommand]
        private async Task TakePhotoAsync()
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                WeakReferenceMessenger.Default.Send("Camera not supported on this device.");
                return;
            }

            try
            {
                FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo == null) return;

                IsProcessing = true;

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

                PreviewImage = ImageSource.FromFile(cachePath);

                _detectedGame = await _imageProcessor.ProcessImageAsync(cachePath);
                _suggestedMove = await _solver.GetBestMoveAsync(_detectedGame);

                IsProcessing = false;

                ShowGoButton = true;

            }
            catch (Exception ex)
            {
                IsProcessing = false;
                WeakReferenceMessenger.Default.Send($"Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task GoToPlayAsync()
        {
            if (_detectedGame == null)
                return;

            string gameJson = JsonSerializer.Serialize(_detectedGame);

            await Shell.Current.GoToAsync(
                $"///play?game={Uri.EscapeDataString(gameJson)}" +
                $"&row={_suggestedMove.row}&col={_suggestedMove.col}" +
                $"&photoPath={Uri.EscapeDataString(_photoPathPersistent)}"
            );
        }
    }
}
