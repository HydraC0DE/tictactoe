using Microsoft.Extensions.Logging;
using tictactoe.Services;
using tictactoe.Models;
using tictactoe.Data;
using tictactoe.Views;
using tictactoe.ViewModels;

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
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "matches.db");

            builder.Services.AddSingleton<ITicTacToeSolver, TicTacToeSolver>();
            builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
            builder.Services.AddSingleton(new MatchRepository(dbPath));
            builder.Services.AddSingleton<PlayPage>(sp =>
            {
                var repo = sp.GetRequiredService<MatchRepository>();
                var solver = sp.GetRequiredService<ITicTacToeSolver>();
                return new PlayPage(repo, solver);
            });

            builder.Services.AddTransient<MatchDetailPage>();
            builder.Services.AddTransient<MatchDetailPageViewModel>();
            builder.Services.AddTransient<HistoryPage>();
            builder.Services.AddTransient<HistoryPageViewModel>();
            builder.Services.AddTransient<PicturePage>();
            builder.Services.AddTransient<PicturePageViewModel>();



#if DEBUG
            builder.Logging.AddDebug();
#endif


            return builder.Build();
        }
    }
}
