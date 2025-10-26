using tictactoe.Views;
namespace tictactoe
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(MatchDetailPage), typeof(MatchDetailPage));
        }
    }
}
