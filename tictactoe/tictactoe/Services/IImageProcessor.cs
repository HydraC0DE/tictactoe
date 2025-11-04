using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tictactoe.Models;

namespace tictactoe.Services
{
    public interface IImageProcessor
    {
        Task<Game> ProcessImageAsync(string boardImagePath);
    }
}
