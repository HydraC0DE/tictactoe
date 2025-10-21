namespace tictactoe.Views;
using tictactoe.Services;

public partial class PicturePage : ContentPage
{
    private readonly ITicTacToeSolver _solver;
    private string _solverResult; // store the result for PlayPage

    public PicturePage(ITicTacToeSolver solver)
    {
        InitializeComponent();
        _solver = solver;
    }

    private async Task TakePhotoAsync()
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo != null)
            {
                string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);

                using Stream sourceStream = await photo.OpenReadAsync();
                using FileStream localFileStream = File.OpenWrite(localFilePath);

                await sourceStream.CopyToAsync(localFileStream);

                // Optional: show the image in the UI
                imgPreview.Source = ImageSource.FromFile(localFilePath);
            }
        }
    }

    private async void OnCameraClicked(object sender, EventArgs e)
    {
        await TakePhotoAsync();
    }


    private async void OnGoToPlayClicked(object sender, EventArgs e)
    {
        // Pass the solver result to PlayPage via query parameters
        if (!string.IsNullOrEmpty(_solverResult))
        {
            await Shell.Current.GoToAsync($"play?bestMove={_solverResult}");
        }
    }
}