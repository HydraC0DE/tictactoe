using Microsoft.Extensions.Logging;
using tictactoe.Services;
using tictactoe.Models;
using tictactoe.Data;
using tictactoe.Views;

namespace tictactoe
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<ITicTacToeSolver, TicTacToeSolver>();
            builder.Services.AddSingleton<MatchRepository>();
            builder.Services.AddSingleton<HistoryPage>();
            builder.Services.AddSingleton<PlayPage>();



            return builder.Build();
        }
    }
}
