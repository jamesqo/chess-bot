using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses alpha-beta search to pick the best move.
    /// </summary>
    public class AlphaBeta : IMovePicker<AlphaBeta.Info>
    {
        public class Info
        {
            internal Info(int utility) => Utility = utility;

            public int Utility { get; }
        }

        private readonly struct TtEntry
        {
            public TtEntry(int utilityEstimate, int depth)
            {
                Debug.Assert(depth > 0);

                UtilityEstimate = utilityEstimate;
                Depth = depth;
            }

            public int UtilityEstimate { get; }
            public int Depth { get; }
        }

        private readonly TranspositionTable<TtEntry> _tt;

        public AlphaBeta(int depth)
        {
            Depth = depth;
            _tt = new TranspositionTable<TtEntry>();
        }

        public int Depth { get; set; }

        public Move PickMove(State state, out Info info)
        {
            Move bestMove = default;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            var (alpha, beta) = (int.MinValue, int.MaxValue);
            bool isTerminal = true;

            foreach (var (move, succ) in state.GetSuccessors())
            {
                isTerminal = false;

                int value = DoAlphaBeta(succ, Depth - 1, alpha, beta);
                if (state.WhiteToMove)
                {
                    bool better = (value > bestValue);
                    if (better)
                    {
                        bestValue = value;
                        bestMove = move;
                        alpha = Math.Max(alpha, bestValue);
                    }
                }
                else
                {
                    bool better = (value < bestValue);
                    if (better)
                    {
                        bestValue = value;
                        bestMove = move;
                        beta = Math.Min(beta, bestValue);
                    }
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            if (isTerminal)
            {
                throw new ArgumentException($"A terminal state was passed to {nameof(PickMove)}", nameof(state));
            }

            info = new Info(utility: bestValue);
            return bestMove;
        }

        private int _AlphaBeta(State state, int d, int alpha, int beta)
        {
            Debug.Assert(alpha < beta);

            if (d == 0)
            {
                return Evaluation.Heuristic(state);
            }
            TtEntry tte;
            if (_tt.TryGetNode(state, out var ttNode))
            {
                tte = ttNode.Value;
                if (tte.Depth >= d)
                {
                    _tt.Touch(ttNode);
                    return tte.UtilityEstimate;
                }
            }

            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;

            foreach (var (_, succ) in state.GetSuccessors())
            {
                isTerminal = false;

                int value = DoAlphaBeta(succ, d - 1, alpha, beta);
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

            tte = new TtEntry(utilityEstimate: bestValue, depth: d);
            if (ttNode != null && !ttNode.WasEvicted) // the node could have been evicted during a recursive call
            {
                // update the existing node
                ttNode.Value = tte;
                _tt.Touch(ttNode);
            }
            else
            {
                _tt.Add(state, tte);
            }

            return bestValue;
        }
    }
}
