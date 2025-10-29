using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tictactoe.Models;

namespace tictactoe.Services
{
    public class TicTacToeSolver : ITicTacToeSolver //Mock
    {
        public async Task<Game> ProcessImageAsync(string imagePath)
        {
            // Mock: return empty board
            await Task.Delay(100); // simulate processing delay
            return new Game();
        }

        public async Task<(int row, int col)> GetBestMoveAsync(Game game)
        {
            // Mock: always suggest center
            await Task.Delay(50);
            return (1, 1);
        }
    }
}
