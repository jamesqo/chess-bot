using ChessBot.Types;
using System;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses alpha-beta search to pick the best move.
    /// </summary>
    public class AlphaBetaPicker : IMovePicker
    {
        private readonly int _depth;
        private readonly TranspositionTable<int> _tt;

        public AlphaBetaPicker(int depth)
        {
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));

            _depth = depth;
            _tt = new TranspositionTable<int>();
        }

        public Move PickMove(State state)
        {
            Move bestMove = default;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            var (alpha, beta) = (int.MinValue, int.MaxValue);
            bool isTerminal = true;

            using var movesAndSuccs = state.GetMovesAndSuccessors();
            foreach (var (move, succ) in movesAndSuccs)
            {
                isTerminal = false;

                int value = AlphaBeta(succ, _depth - 1, alpha, beta);
                if (state.WhiteToMove)
                {
                    bool better = (value > bestValue);
                    if (better)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                    alpha = Math.Max(alpha, bestValue);
                }
                else
                {
                    bool better = (value < bestValue);
                    if (better)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                    beta = Math.Min(beta, bestValue);
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            return isTerminal
                ? throw new ArgumentException($"A terminal state was passed to {nameof(PickMove)}", nameof(state))
                : bestMove;
        }

        private int AlphaBeta(State state, int d, int alpha, int beta)
        {
            if (d == 0)
            {
                return Evaluation.Heuristic(state);
            }
            if (_tt.TryGetValue(state, out int cachedValue))
            {
                return cachedValue;
            }

            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;

            using var movesAndSuccs = state.GetMovesAndSuccessors();
            foreach (var (_, succ) in movesAndSuccs)
            {
                isTerminal = false;

                int value = AlphaBeta(succ, d - 1, alpha, beta);
                if (state.WhiteToMove)
                {
                    bestValue = Math.Max(bestValue, value);
                    alpha = Math.Max(alpha, bestValue);
                }
                else
                {
                    bestValue = Math.Min(bestValue, value);
                    beta = Math.Min(beta, bestValue);
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            if (isTerminal)
            {
                return Evaluation.Terminal(state);
            }

            _tt.TryAdd(state, bestValue);
            return bestValue;
        }
    }
}
