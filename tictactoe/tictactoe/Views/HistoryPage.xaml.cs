using tictactoe.ViewModels;
using tictactoe.Data;

namespace tictactoe.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryPageViewModel _vm;

    public HistoryPage(MatchRepository repo)
    {
        InitializeComponent();
        _vm = (HistoryPageViewModel)BindingContext;
        _vm = new HistoryPageViewModel(repo);
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
