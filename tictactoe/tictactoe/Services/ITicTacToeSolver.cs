using Android.Bluetooth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tictactoe.Models;

namespace tictactoe.Services
{
    public interface ITicTacToeSolver
    {
        Task<Game> ProcessImageAsync(string boardImagePath);

        Task<(int row, int col)> GetBestMoveAsync(Game game);
    }


}
