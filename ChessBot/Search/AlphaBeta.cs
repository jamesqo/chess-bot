using ChessBot.Helpers;
using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    // todo: implement killer move heuristic for this searcher
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

        private readonly struct TtEntry : IHasDepth
        {
            public TtEntry(int utilityEstimate, int depth)
            {
                Debug.Assert(depth > 0);

                UtilityEstimate = utilityEstimate;
                Depth = depth;
            }

            public int UtilityEstimate { get; }
            public int Depth { get; }

            public override string ToString()
            {
                var sb = StringBuilderCache.Acquire();
                sb.Append(UtilityEstimate);
                sb.Append(", ");
                sb.Append(nameof(Depth));
                sb.Append(" = ");
                sb.Append(Depth);
                return StringBuilderCache.GetStringAndRelease(sb);
            }
        }

        private readonly ITranspositionTable<TtEntry> _tt;

        public AlphaBeta(int depth)
        {
            Depth = depth;
            _tt = new LruReplacementTt<TtEntry>();
            // todo: figure out why this isn't performing any better than plain old lru replacement for alphabeta?
            //_tt = new TwoTierReplacementTt<TtEntry>();
        }

        public int Depth { get; set; }

        public Move PickMove(State root, out Info info)
        {
            Log.Debug("Entering {0}.{1}", arg0: nameof(AlphaBeta), arg1: nameof(PickMove));

            Move bestMove = default;
            int bestValue = root.WhiteToMove ? int.MinValue : int.MaxValue;
            var (alpha, beta) = (int.MinValue, int.MaxValue);
            bool isTerminal = true;
            var state = root.ToMutable();

            foreach (var move in state.GetPseudoLegalMoves())
            {
                if (!state.TryApply(move, out _)) continue;

                isTerminal = false;

                int value = _AlphaBeta(state, Depth - 1, alpha, beta);
                state.Undo();

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
                throw new ArgumentException($"A terminal state was passed to {nameof(PickMove)}", nameof(root));
            }

            info = new Info(utility: bestValue);
            Log.Debug("Computed {0} as the minimax value for {1}", info.Utility, root);
            Log.Debug("Exiting {0}.{1}", arg0: nameof(AlphaBeta), arg1: nameof(PickMove));
            return bestMove;
        }

        private int _AlphaBeta(MutState state, int d, int alpha, int beta)
        {
            Debug.Assert(alpha < beta);
            Debug.Assert(d >= 0 && d < Depth);

            if (d == 0)
            {
                return Evaluation.Heuristic(state);
            }

            TtEntry tte;
            var ttRef = _tt.TryGetReference(state.Hash);
            if (ttRef != null)
            {
                tte = ttRef.Value;
                if (tte.Depth >= d)
                {
                    _tt.Touch(ttRef);
                    return tte.UtilityEstimate;
                }
            }

            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            int childrenSearched = 0;

            Log.Debug("Commencing search of children of state {0}", state);
            Log.IndentLevel++;
            foreach (var move in state.GetPseudoLegalMoves())
            {
                if (!state.TryApply(move, out _)) continue;

                childrenSearched++;

                int value = _AlphaBeta(state, d - 1, alpha, beta);
                state.Undo();

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
                    if (state.WhiteToMove)
                    {
                        Log.Debug("Beta cutoff occurred with alpha={0} beta={1}", alpha, beta);
                    }
                    else
                    {
                        Log.Debug("Alpha cutoff occurred with alpha={0} beta={1}", alpha, beta);
                    }
                    break;
                }
            }
            Log.IndentLevel--;

            if (childrenSearched == 0)
            {
                return Evaluation.Terminal(state);
            }
            Log.Debug("Searched {0} children of state {1}", childrenSearched, state);

            tte = new TtEntry(utilityEstimate: bestValue, depth: d);
            _tt.UpdateOrAdd(ttRef, state.Hash, tte);
            return bestValue;
        }
    }
}
