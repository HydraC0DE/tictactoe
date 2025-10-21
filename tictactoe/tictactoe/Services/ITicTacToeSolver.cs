using Android.Bluetooth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tictactoe.Services
{
    public interface ITicTacToeSolver
    {
        Task<string> GetBestMoveAsync(string boardImagePath);
    }

    //public class TicTacToeSolverMock : ITicTacToeSolver
    //{
    //    public Move GetBestMove(Board board)
    //    {
    //        return new Move(0, 0); // mock example
    //    }
    //}
}
