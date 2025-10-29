using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tictactoe.Models
{
    public class Game
    {
        public string[,] Board { get; set; } = new string[3, 3];

        public string NextMove { get; set; } = "X"; // X starts the game
        public bool IsTerminal { get; set; } = false;

        public string Result { get; set; } = "Not finished";

        public Game()
        {
            //empty field
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Board[i, j] = string.Empty;
                }
            }
               
        }

        public bool MakeMove(int row, int col)
        {
            if (IsTerminal || !string.IsNullOrEmpty(Board[row, col]))
                return false; // invalid move

            Board[row, col] = NextMove;

            // Check if game is over
            if (CheckWin(NextMove))
            {
                Result = NextMove;
                IsTerminal = true;
            }
            else if (IsBoardFull())
            {
                Result = "Draw";
                IsTerminal = true;
            }
            else
            {
                NextMove = NextMove == "X" ? "O" : "X";
            }

            return true;
        }

        private bool IsBoardFull()
        {
            return Board.Cast<string>().All(cell => !string.IsNullOrEmpty(cell));
        }

        private bool CheckWin(string player)
        {
            // Rows, columns, diagonals
            for (int i = 0; i < 3; i++)
            {
                if (Board[i, 0] == player && Board[i, 1] == player && Board[i, 2] == player) return true;
                if (Board[0, i] == player && Board[1, i] == player && Board[2, i] == player) return true;
            }

            return (Board[0, 0] == player && Board[1, 1] == player && Board[2, 2] == player) ||
                   (Board[0, 2] == player && Board[1, 1] == player && Board[2, 0] == player);
        }
    }
}
