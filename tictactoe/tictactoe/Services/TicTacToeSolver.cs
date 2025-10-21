using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tictactoe.Services
{
    public class TicTacToeSolver : ITicTacToeSolver //Mock
    {
        public Task<string> GetBestMoveAsync(string boardImagePath)
        {
            // Mock: always return "Center"
            return Task.FromResult("Center");
        }
    }
}
