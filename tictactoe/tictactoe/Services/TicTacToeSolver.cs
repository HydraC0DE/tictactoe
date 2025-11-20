using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tictactoe.Models;

namespace tictactoe.Services
{
    enum EntryType { Exact, LowerBound, UpperBound }
    struct MoveRecord
    {
        public int Row, Col;
        public int PrevValue;
        public string PrevNextMove;
        public bool PrevIsTerminal;
        public string PrevResult;
        public ulong PrevZobrist;
    }

    class TTEntry
    {
        public ulong Key;
        public double Value;
        public int Depth;
        public EntryType Type;
        public (int r, int c)? BestMove;
    }

    public class TicTacToeSolver : ITicTacToeSolver
    {
        Game CurrentGame;
        private const int DEFAULT_DEPTH = 3;
        private const int CANDIDATE_LIMIT = 30;
        private static readonly Random Rnd = new Random();

        private readonly ulong[,,] zobrist = new ulong[Game.SIZE, Game.SIZE, 3];
        private readonly Dictionary<ulong, TTEntry> transposition = new Dictionary<ulong, TTEntry>(1 << 20);

        public long EvaluationCount { get; private set; } = 0;
        public double LastSearchSeconds { get; private set; } = 0.0;

        public TicTacToeSolver() 
        {
            var rng = new Random(123456);
            for (int r = 0; r < Game.SIZE; r++)
                for (int c = 0; c < Game.SIZE; c++)
                    for (int p = 0; p < 3; p++)
                        zobrist[r, c, p] = ((ulong)rng.Next() << 32) | (uint)rng.Next();
        }

        private ulong ComputeFullZobrist(Game g)
        {
            ulong h = 0;
            for (int r = 0; r < Game.SIZE; r++)
                for (int c = 0; c < Game.SIZE; c++)
                {
                    int val = g.Board[r, c];
                    if (val != 0) h ^= zobrist[r, c, val];
                }
            g.ZobristHash = h;
            return h;
        }

        private MoveRecord ApplyMove(Game g, int row, int col)
        {
            var rec = new MoveRecord
            {
                Row = row,
                Col = col,
                PrevValue = g.Board[row, col],
                PrevNextMove = g.NextMove,
                PrevIsTerminal = g.IsTerminal,
                PrevResult = g.Result,
                PrevZobrist = g.ZobristHash
            };

            int piece = g.NextMove == "X" ? 1 : 2;
            g.Board[row, col] = piece;
            g.ZobristHash ^= zobrist[row, col, piece];
            g.NextMove = g.NextMove == "X" ? "O" : "X";

            if (g.CheckWinAround(row, col, piece))
            {
                g.IsTerminal = true;
                g.Result = piece == 1 ? "X" : "O";
            }
            else if (!g.Board.Cast<int>().Any(x => x == 0))
            {
                g.IsTerminal = true;
                g.Result = "Draw";
            }

            return rec;
        }

        private void UnapplyMove(Game g, MoveRecord rec)
        {
            g.Board[rec.Row, rec.Col] = rec.PrevValue;
            g.NextMove = rec.PrevNextMove;
            g.IsTerminal = rec.PrevIsTerminal;
            g.Result = rec.PrevResult;
            g.ZobristHash = rec.PrevZobrist;
        }

        public double EvaluateBoardSigned(Game g)
        {
            EvaluationCount++;
            double xScore = GetScore(g, true);
            double oScore = GetScore(g, false);

            if (xScore <= 0) xScore = 1.0;
            if (oScore <= 0) oScore = 1.0;

            return Math.Log(xScore / oScore);
        }

        private double GetScore(Game g, bool isX)
        {
            var patterns = new Dictionary<string, int>();
            for (int r = 0; r < Game.SIZE; r++)
            {
                var arr = new int[Game.SIZE];
                for (int c = 0; c < Game.SIZE; c++) arr[c] = g.Board[r, c];
                ScanLine(arr, patterns, isX);
            }
            for (int c = 0; c < Game.SIZE; c++)
            {
                var arr = new int[Game.SIZE];
                for (int r = 0; r < Game.SIZE; r++) arr[r] = g.Board[r, c];
                ScanLine(arr, patterns, isX);
            }
            for (int d = -Game.SIZE + 1; d <= Game.SIZE - 1; d++)
            {
                var diag1 = new List<int>();
                var diag2 = new List<int>();
                for (int r = 0; r < Game.SIZE; r++)
                {
                    int c1 = r + d;
                    if (c1 >= 0 && c1 < Game.SIZE) diag1.Add(g.Board[r, c1]);
                    int c2 = Game.SIZE - 1 - r - d;
                    if (c2 >= 0 && c2 < Game.SIZE) diag2.Add(g.Board[r, c2]);
                }
                if (diag1.Count > 0) ScanLine(diag1.ToArray(), patterns, isX);
                if (diag2.Count > 0) ScanLine(diag2.ToArray(), patterns, isX);
            }
            return ScorePatterns(patterns);
        }

        private void ScanLine(int[] line, Dictionary<string, int> patterns, bool isX)
        {
            int color = isX ? 1 : 2;
            int neg = isX ? 2 : 1;
            string s = "";
            int old = 0;
            for (int i = 0; i < line.Length; i++)
            {
                int c = line[i];
                if (i == 0) s += 'O';
                if (c == color)
                {
                    if (old == neg) s += 'O';
                    s += 'X';
                }
                if (c != color || i == line.Length - 1)
                {
                    if (c == neg && s.Length > 0) s += 'O';
                    else if (i == line.Length - 1) s += 'O';

                    if (s.Length > 0)
                    {
                        if (patterns.ContainsKey(s)) patterns[s]++;
                        else patterns[s] = 1;
                    }
                    s = "";
                }
                old = c;
            }
        }

        private double ScorePatterns(Dictionary<string, int> patterns)
        {
            double score = 0;
            foreach (var kvp in patterns)
            {
                string p = kvp.Key;
                int cnt = kvp.Value;
                int xCount = p.Count(ch => ch == 'X');
                switch (xCount)
                {
                    case 5: if (!(p[0] == 'O' && p[^1] == 'O')) score += 100_000; break;
                    case 4:
                        if (p[0] != 'O' && p[^1] != 'O') score += 10_000 * cnt;
                        else if (p[0] == 'O' ^ p[^1] == 'O') score += 5_000 * cnt;
                        break;
                    case 3:
                        if (p[0] != 'O' && p[^1] != 'O') score += 1_000 * cnt;
                        else if (p[0] == 'O' ^ p[^1] == 'O') score += 500 * cnt;
                        break;
                    case 2:
                        if (p[0] != 'O' && p[^1] != 'O') score += 100 * cnt;
                        else if (p[0] == 'O' ^ p[^1] == 'O') score += 50 * cnt;
                        break;
                    case 1: if (!(p[0] == 'O' && p[^1] == 'O')) score += 1 * cnt; break;
                }
            }
            return score;
        }

        private List<(int r, int c)> HeuristicSortMoves(Game g, List<(int r, int c)> moves)
        {
            int size = Game.SIZE;
            int ScoreMove((int r, int c) mv)
            {
                int x = mv.r, y = mv.c;
                int count = 0;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int nr = x + dr, nc = y + dc;
                        if (nr >= 0 && nr < size && nc >= 0 && nc < size && g.Board[nr, nc] != 0) count++;
                    }
                return count;
            }

            return moves.OrderByDescending(m => ScoreMove(m)).ToList();
        }

        public (double value, (int r, int c)? move) MinimaxRoot(Game g, int depth, double alpha, double beta, bool isMax)
        {
            if (g.ZobristHash == 0) ComputeFullZobrist(g);

            if (g.IsTerminal)
            {
                if (g.Result == "X") return (double.PositiveInfinity, null);
                if (g.Result == "O") return (double.NegativeInfinity, null);
                return (0.0, null);
            }

            var moves = g.GetValidMoves();
            if (moves.Count == 0) return (EvaluateBoardSigned(g), null);

            moves = HeuristicSortMoves(g, moves);
            if (moves.Count > CANDIDATE_LIMIT) moves = moves.Take(CANDIDATE_LIMIT).ToList();

            (int r, int c)? bestMove = null;
            double bestValue = isMax ? double.NegativeInfinity : double.PositiveInfinity;

            foreach (var mv in moves)
            {
                var rec = ApplyMove(g, mv.r, mv.c);

                double val;
                if (g.IsTerminal || depth - 1 == 0) val = EvaluateBoardSigned(g);
                else val = MinimaxValue(g, depth - 1, alpha, beta, !isMax);

                UnapplyMove(g, rec);

                if (isMax)
                {
                    if (val > bestValue) { bestValue = val; bestMove = mv; }
                    alpha = Math.Max(alpha, val);
                    if (alpha >= beta) break;
                }
                else
                {
                    if (val < bestValue) { bestValue = val; bestMove = mv; }
                    beta = Math.Min(beta, val);
                    if (beta <= alpha) break;
                }
            }

            return (bestValue, bestMove);
        }

        private double MinimaxValue(Game g, int depth, double alpha, double beta, bool isMax)
        {
            if (transposition.TryGetValue(g.ZobristHash, out var entry) && entry.Depth >= depth)
            {
                if (entry.Type == EntryType.Exact) return entry.Value;
                if (entry.Type == EntryType.LowerBound && entry.Value > alpha) alpha = entry.Value;
                if (entry.Type == EntryType.UpperBound && entry.Value < beta) beta = entry.Value;
                if (alpha >= beta) return entry.Value;
            }

            if (g.IsTerminal || depth == 0) return EvaluateBoardSigned(g);

            var moves = g.GetValidMoves();
            if (moves.Count == 0) return EvaluateBoardSigned(g);

            moves = HeuristicSortMoves(g, moves);
            if (moves.Count > CANDIDATE_LIMIT) moves = moves.Take(CANDIDATE_LIMIT).ToList();

            double bestValue = isMax ? double.NegativeInfinity : double.PositiveInfinity;
            (int r, int c)? bestMove = null;

            double alphaOrig = alpha;
            double betaOrig = beta;

            foreach (var mv in moves)
            {
                var rec = ApplyMove(g, mv.r, mv.c);
                double val = MinimaxValue(g, depth - 1, alpha, beta, !isMax);
                UnapplyMove(g, rec);

                if (isMax)
                {
                    if (val > bestValue) { bestValue = val; bestMove = mv; }
                    alpha = Math.Max(alpha, val);
                    if (alpha >= beta) break;
                }
                else
                {
                    if (val < bestValue) { bestValue = val; bestMove = mv; }
                    beta = Math.Min(beta, val);
                    if (beta <= alpha) break;
                }
            }

            var store = new TTEntry { Key = g.ZobristHash, Value = bestValue, Depth = depth, BestMove = bestMove };
            if (bestValue <= alphaOrig) store.Type = EntryType.UpperBound;
            else if (bestValue >= betaOrig) store.Type = EntryType.LowerBound;
            else store.Type = EntryType.Exact;

            transposition[g.ZobristHash] = store;
            return bestValue;
        }
        //public (int row, int col)? FindImmediateWin(Game g)
        //{
        //    int aiPiece = g.NextMove == "X" ? 1 : 2;

        //    foreach (var (r, c) in g.GetValidMoves())
        //    {
        //        var rec = ApplyMove(g, r, c);

        //        // Check if this move creates a win for the AI
        //        if (g.IsTerminal && g.Result == g.NextMove)
        //        {
        //            UnapplyMove(g, rec);
        //            ;
        //            return (r, c);
        //        }

        //        // Alternate approach: check locally without relying on g.Result
        //        if (g.CheckWinAround(r, c, aiPiece))
        //        {
        //            UnapplyMove(g, rec);
        //            ;
        //            return (r, c);
        //        }
        //        else
        //        {
        //            UnapplyMove(g, rec);
        //        }

        //    }

        //    return null; // no immediate win found
        //}

            public (int row, int col)? FindImmediateWin(Game g)
        {
            int aiPiece = g.NextMove == "X" ? 1 : 2;

            foreach (var (r, c) in g.GetValidMoves())
            {
                var rec = ApplyMove(g, r, c);

                bool isWin = (g.IsTerminal && g.Result == g.NextMove) || g.CheckWinAround(r, c, aiPiece);

                UnapplyMove(g, rec); // always unapply exactly once

                if (isWin)
                {
                    
                    return (r, c);
                }
                    
            }

            return null; // no immediate win found
        }


        public async Task<(int row, int col)> GetBestMoveAsync(Game game)
        {
            if (game.NextMove == "O")
            {
                var winMove = FindImmediateWin(game);
                if (winMove.HasValue)
                {
                    
                    return winMove.Value;
                }
            }

            bool isXTurn = (game.NextMove == "X");
            int depth = DEFAULT_DEPTH;
            EvaluationCount = 0;
            LastSearchSeconds = 0;
            transposition.Clear();

            var sw = Stopwatch.StartNew();

            var result = await Task.Run(() =>
            {
                var (value, move) = MinimaxRoot(game, depth, double.NegativeInfinity, double.PositiveInfinity, isXTurn);
                if (move.HasValue) return move.Value;
                var valid = game.GetValidMoves();
                if (valid.Count > 0) return valid[Rnd.Next(valid.Count)];
                return (0, 0);
            });

            sw.Stop();
            LastSearchSeconds = sw.Elapsed.TotalSeconds;
            return result;
        }

        
    }

    
}
