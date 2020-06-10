using ChessBot.Helpers;
using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses MTD-f search to pick the best move.
    /// </summary>
    public class Mtdf : IMovePicker<Mtdf.Info>
    {
        public class Info
        {
            internal Info(int utility) => Utility = utility;

            public int Utility { get; }
        }

        private readonly struct TtEntry : IHasDepth
        {
            public TtEntry(int lowerBound, int upperBound, int depth, Move pvMove)
            {
                Debug.Assert(lowerBound <= upperBound);
                Debug.Assert(depth > 0);
                Debug.Assert(pvMove.IsValid);

                LowerBound = lowerBound;
                UpperBound = upperBound;
                Depth = depth;
                PvMove = pvMove;
            }

            public int LowerBound { get; }
            public int UpperBound { get; }
            public int Depth { get; }
            public Move PvMove { get; }

            public override string ToString()
            {
                var sb = StringBuilderCache.Acquire();
                sb.Append('[');
                sb.Append(LowerBound);
                sb.Append(", ");
                sb.Append(UpperBound);
                sb.Append("], ");
                sb.Append(nameof(Depth));
                sb.Append(" = ");
                sb.Append(Depth);
                return StringBuilderCache.GetStringAndRelease(sb);
            }
        }

        private readonly ITranspositionTable<TtEntry, LruNode<TtEntry>> _tt;

        public Mtdf(int depth)
        {
            Depth = depth;
            _tt = new TwoTierReplacementTt<TtEntry>();
        }

        public int Depth { get; set; }
        public int FirstGuess { get; set; } = 0;

        public Move PickMove(State root, out Info info)
        {
            Log.Debug("Entering {0}.{1}", arg0: nameof(Mtdf), arg1: nameof(PickMove));

            Move bestMove = default;
            int bestValue = root.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;
            var state = root.ToMutable();

            foreach (var move in state.GetPseudoLegalMoves())
            {
                if (!state.TryApply(move, out _)) continue;

                isTerminal = false;

                int value = _Mtdf(state, FirstGuess, Depth - 1, _tt, bestValue);
                state.Undo();

                bool better = (root.WhiteToMove ? value > bestValue : value < bestValue);
                if (better)
                {
                    bestValue = value;
                    bestMove = move;
                }
            }

            if (isTerminal)
            {
                throw new ArgumentException($"A terminal state was passed to {nameof(PickMove)}", nameof(root));
            }

            info = new Info(utility: bestValue);
            Log.Debug("Computed {0} as the minimax value for {1}", info.Utility, root);
            Log.Debug("Exiting {0}.{1}", arg0: nameof(Mtdf), arg1: nameof(PickMove));
            return bestMove;
        }

        // todo: now that we're storing moves in each entry, this should return the pv move as well so that PickMove() can use it directly
        private static int _Mtdf(
            MutState root,
            int firstGuess,
            int depth,
            ITranspositionTable<TtEntry, LruNode<TtEntry>> tt,
            int bestSiblingValue)
        {
            int guess = firstGuess;
            var (lowerBound, upperBound) = (int.MinValue, int.MaxValue);

            bool whiteToMoveInParent = !root.WhiteToMove;
            if (whiteToMoveInParent)
            {
                lowerBound = bestSiblingValue;
            }
            else
            {
                upperBound = bestSiblingValue;
            }

            do
            {
                int beta = guess == lowerBound ? (guess + 1) : guess;
                Log.Debug("Starting null-window search for state {0} with beta={1}", root, beta);
                guess = AlphaBetaWithMemory(root, beta - 1, beta, depth, tt);
                Log.Debug("Null-window search for state {0} with beta={1} returned {2}", root, beta, guess);
                if (guess < beta) // alpha-cutoff: tells us that the real value is <= guess
                {
                    upperBound = guess;
                }
                else // beta-cutoff: tells us that the real value is >= guess
                {
                    lowerBound = guess;
                }
                // EXPERIMENTAL: trying binary search
                guess = (int)(((long)lowerBound + (long)upperBound) / 2);
            }
            while (lowerBound < upperBound);

            return guess;
        }

        private static int AlphaBetaWithMemory(
            MutState state,
            int alpha,
            int beta,
            int depth,
            ITranspositionTable<TtEntry, LruNode<TtEntry>> tt)
        {
            Debug.Assert(alpha < beta);
            Debug.Assert(depth >= 0);

            if (depth == 0)
            {
                return Evaluation.Heuristic(state);
            }

            TtEntry tte;
            Move pvMove = default;
            if (tt.TryGetNode(state, out var ttNode))
            {
                tte = ttNode.Value;
                if (tte.Depth >= depth)
                {
                    // beta-cutoff
                    if (tte.LowerBound >= beta)
                    {
                        tt.Touch(ttNode);
                        return tte.LowerBound;
                    }
                    // alpha-cutoff
                    if (tte.UpperBound <= alpha)
                    {
                        tt.Touch(ttNode);
                        return tte.UpperBound;
                    }
                    // we know the exact value
                    if (tte.LowerBound == tte.UpperBound)
                    {
                        tt.Touch(ttNode);
                        return tte.LowerBound;
                    }

                    // use the information to refine our bounds
                    // todo: this may not actually work (see note below)
                    alpha = Math.Max(alpha, tte.LowerBound);
                    beta = Math.Min(beta, tte.UpperBound);
                }
                pvMove = ttNode.Value.PvMove;
                Debug.Assert(!pvMove.IsDefault);
            }

            int guess = state.WhiteToMove ? int.MinValue : int.MaxValue;
            var (a, b) = (alpha, beta);
            bool pvCausesCut = false;
            if (!pvMove.IsDefault)
            {
                bool success = state.TryApply(pvMove, out _);
                Debug.Assert(success);
                guess = AlphaBetaWithMemory(state, alpha, beta, depth - 1, tt);
                state.Undo();
                if (state.WhiteToMove)
                {
                    pvCausesCut = (guess >= beta);
                    if (pvCausesCut) Log.Debug("PV move {0} caused beta cutoff for state {1} with guess={2} beta={3}", pvMove, state, guess, beta);
                    a = Math.Max(a, guess);
                }
                else
                {
                    pvCausesCut = (guess <= alpha);
                    if (pvCausesCut) Log.Debug("PV move {0} caused alpha cutoff for state {1} with guess={2} alpha={3}", pvMove, state, guess, alpha);
                    b = Math.Min(b, guess);
                }
            }

            if (!pvCausesCut)
            {
                int childrenSearched = 0;

                Log.Debug("Commencing search of children of state {0}", state);
                Log.IndentLevel++;
                if (state.WhiteToMove)
                {
                    foreach (var move in state.GetPseudoLegalMoves())
                    {
                        if (!state.TryApply(move, out _)) continue;

                        childrenSearched++;

                        int value = AlphaBetaWithMemory(state, a, beta, depth - 1, tt);
                        state.Undo();
                        bool better = value > guess;
                        if (better)
                        {
                            guess = value;
                            pvMove = move;
                            a = Math.Max(a, guess);

                            if (guess >= beta)
                            {
                                Log.Debug("Beta cutoff occurred with guess={0} beta={1}", guess, beta);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var move in state.GetPseudoLegalMoves())
                    {
                        if (!state.TryApply(move, out _)) continue;

                        childrenSearched++;

                        int value = AlphaBetaWithMemory(state, alpha, b, depth - 1, tt);
                        state.Undo();
                        bool better = value < guess;
                        if (better)
                        {
                            guess = value;
                            pvMove = move;
                            b = Math.Min(b, guess);

                            if (guess <= alpha)
                            {
                                Log.Debug("Alpha cutoff occurred with guess={0} alpha={1}", guess, alpha);
                                break;
                            }
                        }
                    }
                }
                Log.IndentLevel--;

                if (childrenSearched == 0)
                {
                    return Evaluation.Terminal(state);
                }
                Log.Debug("Searched {0} children of state {1}", childrenSearched, state);
            }

            Debug.Assert(!pvMove.IsDefault);
            if (guess <= alpha) // fail-low result => upper bound
            {
                tte = new TtEntry(lowerBound: int.MinValue, upperBound: guess, depth: depth, pvMove: pvMove);
            }
            else // fail-high result => lower bound
            {
                // for now, we're only calling this with null-window searches where alpha == beta - 1
                Debug.Assert(guess >= beta);
                tte = new TtEntry(lowerBound: guess, upperBound: int.MaxValue, depth: depth, pvMove: pvMove);
            }
            /*
            else // neither => this is the exact minimax value
            {
                Debug.Assert(guess > alpha && guess < beta);
                tte = new TtEntry(lowerBound: guess, upperBound: guess);
            }
            */

            if (ttNode != null && !ttNode.WasRemoved) // the node could have been evicted during a recursive call
            {
                int ttDepth = ttNode.Value.Depth;
                if (depth >= ttDepth) // information about higher depths is more valuable
                {
                    if (depth == ttDepth)
                    {
                        bool overlaps = (tte.LowerBound <= ttNode.Value.UpperBound) && (ttNode.Value.LowerBound <= tte.UpperBound);
                        // although rare, this could be false in the following scenario:
                        //
                        // suppose our utility is computed based off of the utility of a child with depth = x. later, the child gets a tt entry with an
                        // associated depth = y. the child entry could differ greatly from its earlier value, which could affect our minimax value (and
                        // make the earlier bounds obsolete) even though we're passing the same depth both times.
                        //
                        // in this case, we just assume the old range is obsolete and replace it entirely.
                        if (overlaps)
                        {
                            // improve on what we already know
                            tte = new TtEntry(
                                Math.Max(tte.LowerBound, ttNode.Value.LowerBound),
                                Math.Min(tte.UpperBound, ttNode.Value.UpperBound),
                                depth: depth,
                                pvMove: pvMove);
                        }
                    }
                    ttNode.Value = tte;
                    tt.Touch(ttNode);
                }
            }
            else
            {
                tt.Add(state, tte);
            }

            return guess;
        }
    }
}
