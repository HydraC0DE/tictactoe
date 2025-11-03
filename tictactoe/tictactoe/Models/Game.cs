using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tictactoe.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Game
    {
        [JsonIgnore]
        public const int SIZE = 15;
        [JsonIgnore]
        public const int K = 5;
        [JsonIgnore]
        public int[,] Board { get; private set; } = new int[SIZE, SIZE];
        [JsonPropertyName("NextMove")]
        public string NextMove { get; internal set; } = "X";
        [JsonPropertyName("IsTerminal")]
        public bool IsTerminal { get; internal set; } = false;
        [JsonPropertyName("Result")]
        public string Result { get; internal set; } = "Not finished";
        [JsonIgnore]
        internal ulong ZobristHash = 0;

        //json seralizable setter and getter because it cant handle normal 2d arrays
        [JsonPropertyName("SerializableBoard")]
        public int[][] SerializableBoard
        {
            get
            {
                var jagged = new int[SIZE][];
                for (int r = 0; r < SIZE; r++)
                {
                    jagged[r] = new int[SIZE];
                    for (int c = 0; c < SIZE; c++)
                        jagged[r][c] = Board[r, c];
                }
                return jagged;
            }
            set
            {
                
                if (value == null || value.Length != SIZE)
                    throw new ArgumentException($"Invalid board data — expected {SIZE} rows.");

                var newBoard = new int[SIZE, SIZE];
                for (int r = 0; r < SIZE; r++)
                {
                    if (value[r] == null || value[r].Length != SIZE)
                        throw new ArgumentException($"Invalid board row at index {r} — expected {SIZE} columns.");
                    for (int c = 0; c < SIZE; c++)
                        newBoard[r, c] = value[r][c];
                }
                
                // --- Validate loaded state ---
                if (!ValidateBoardState(newBoard))
                    throw new InvalidOperationException("Invalid board state: violates game rules (adjacency or count).");

                Board = newBoard;
            }
        }

        private bool ValidateBoardState(int[,] board)//not using the already existing isadjacent because that relies on the non serialized board
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (board[r, c] < 0 || board[r, c] > 2)
                        return false;

            var moves = new List<(int r, int c)>();
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (board[r, c] != 0)
                        moves.Add((r, c));

            if (moves.Count == 0)
                return true;

            foreach (var (r, c) in moves)
            {
                bool adjacent = false;
                for (int dr = -1; dr <= 1 && !adjacent; dr++)
                    for (int dc = -1; dc <= 1 && !adjacent; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = r + dr, nc = c + dc;
                        if (InBounds(nr, nc) && board[nr, nc] != 0)
                            adjacent = true;
                    }

                if (!adjacent && moves.Count > 1)
                    return false;
            }

            return true;
        }
        [JsonConstructor]
        public Game(string nextMove, bool isTerminal, string result, int[][] serializableBoard)
        {
            NextMove = nextMove;
            IsTerminal = isTerminal;
            Result = result;

            SerializableBoard = serializableBoard;
        }
        public Game(bool placeInitialX = true)
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    Board[r, c] = 0;

            if (placeInitialX)
            {
                int mid = SIZE / 2;
                Board[mid, mid] = 1;
                NextMove = "O";
            }
        }

        //public bool MakeMove(int row, int col)
        //{
        //    if (!InBounds(row, col)) return false;
        //    if (IsTerminal) return false;
        //    if (Board[row, col] != 0) return false;
        //    if (!HasAnyPiece() || IsAdjacent(row, col))
        //    {
        //        Board[row, col] = NextMove == "X" ? 1 : 2;
        //        if (CheckWinAround(row, col, Board[row, col]))
        //        {
        //            ;
        //            Result = NextMove;
        //            IsTerminal = true;
        //        }
        //        else if (!MovesLeft())
        //        {
        //            Result = "Draw";
        //            IsTerminal = true;
        //        }
        //        else
        //        {
        //            NextMove = NextMove == "X" ? "O" : "X";
        //        }
        //        return true;
        //    }
        //    return false;
        //}

        public bool MakeMove(int row, int col)
        {
            if (!InBounds(row, col)) return false;
            if (IsTerminal) return false;
            if (Board[row, col] != 0) return false;

            if (!HasAnyPiece() || IsAdjacent(row, col))
            {
                // determine piece to place and apply it
                int placed = NextMove == "X" ? 1 : 2;
                Board[row, col] = placed;

                // Check win using the placed piece (avoid depending on NextMove string)
                if (CheckWinAround(row, col, placed))
                {
                    Result = placed == 1 ? "X" : "O"; // base result on placed piece
                    IsTerminal = true;
                }
                else if (!MovesLeft())
                {
                    Result = "Draw";
                    IsTerminal = true;
                }
                else
                {
                    // only toggle NextMove if game continues
                    NextMove = NextMove == "X" ? "O" : "X";
                }

                return true;
            }

            return false;
        }


        public bool IsAdjacent(int row, int col)
        {
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = row + dr, nc = col + dc;
                    if (InBounds(nr, nc) && Board[nr, nc] != 0) return true;
                }
            return false;
        }

        public bool HasAnyPiece()
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] != 0) return true;
            return false;
        }

        private bool MovesLeft()
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] == 0) return true;
            return false;
        }

        public bool InBounds(int r, int c) => r >= 0 && r < SIZE && c >= 0 && c < SIZE;

        public bool CheckWinAround(int row, int col, int player)
        {
            if (player == 0) return false;

            
            (int dr, int dc)[] dirs = { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dr, dc) in dirs)
            {
                int count = 1;
                int r = row + dr, c = col + dc;
                while (InBounds(r, c) && Board[r, c] == player) { count++; r += dr; c += dc; }
                r = row - dr; c = col - dc;
                while (InBounds(r, c) && Board[r, c] == player) { count++; r -= dr; c -= dc; }
                if (count >= K)
                {
                    
                    
                    return true;
                }
                

            }
            return false;
        }

        public List<(int r, int c)> GetValidMoves()
        {
            var moves = new List<(int, int)>();
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] == 0 && IsAdjacent(r, c))
                        moves.Add((r, c));

            if (moves.Count == 0 && !HasAnyPiece())
            {
                for (int r = 0; r < SIZE; r++)
                    for (int c = 0; c < SIZE; c++)
                        if (Board[r, c] == 0) moves.Add((r, c));
            }

            return moves;
        }

        public void PrintBoard()
        {
            for (int r = 0; r < SIZE; r++)
            {
                for (int c = 0; c < SIZE; c++)
                {
                    char ch = Board[r, c] == 0 ? '.' : (Board[r, c] == 1 ? 'X' : 'O');
                    Console.Write(ch);
                }
                Console.WriteLine();
            }
        }
    }
}