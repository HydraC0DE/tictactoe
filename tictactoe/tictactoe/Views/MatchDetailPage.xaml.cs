using tictactoe.ViewModels;
using tictactoe.Data;
using tictactoe.Models;

namespace tictactoe.Views;

public partial class MatchDetailPage : ContentPage
{
    private Match _match;
    private readonly MatchRepository _repo;
    public MatchDetailPage(MatchDetailPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }




}

