using CommunityToolkit.Mvvm.Messaging;
using tictactoe.ViewModels;

namespace tictactoe.Views;

public partial class PicturePage : ContentPage
{
    private readonly PicturePageViewModel _viewModel;

    public PicturePage(PicturePageViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;
    }


}
