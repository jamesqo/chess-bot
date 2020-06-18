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
    public class Mtdf : ISearchAlgorithm
    {
        public class Info
        {
            internal Info(int score) => Score = score;

            public int Score { get; }
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

        private readonly ITranspositionTable<TtEntry> _tt;
        private readonly Stopwatch _sw;

        public Mtdf(int ttCapacity)
        {
            _tt = new TwoTierReplacementTt<TtEntry>(ttCapacity);
            _sw = new Stopwatch();
        }

        public int Depth { get; set; } = 0;
        public int FirstGuess { get; set; } = 0;

        public ISearchInfo Search(State root)
        {
            if (Depth <= 0)
            {
                throw new InvalidOperationException("Depth wasn't set");
            }

            Log.Debug("Starting MTD-f search");

            Move bestMove = default;
            int bestValue = root.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;
            var state = root.ToMutable();

            foreach (var move in state.GetPseudoLegalMoves())
            {
                if (!state.TryApply(move, out _)) continue;

                isTerminal = false;

                int value = RunMtdf(state, FirstGuess, Depth - 1, _tt, bestValue);
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
                throw new ArgumentException($"A terminal state was passed to {nameof(Search)}", nameof(root));
            }

            Log.Debug("Computed {0} as the minimax value for {1}", bestValue, root);
            Log.Debug("Finished MTD-f search");
            return new SearchInfo(
                depth: Depth,
                elapsed: _sw.Elapsed,
                nodesSearched: default /* todo */,
                pv: default /* todo */,
                score: bestValue);
        }

        // todo: now that we're storing moves in each entry, this should return the pv move as well so that PickMove() can use it directly
        private static int RunMtdf(
            MutState root,
            int firstGuess,
            int depth,
            ITranspositionTable<TtEntry> tt,
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
                var unused = KillerMoves.Empty;
                guess = NullWindowSearch(root, beta, depth, tt, killers: ref unused);
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

        // does alpha-beta search on the null window [beta-1, beta] using a transposition table
        private static int NullWindowSearch(
            MutState state,
            int beta,
            int depth,
            ITranspositionTable<TtEntry> tt,
            ref KillerMoves killers)
        {
            Debug.Assert(depth >= 0);

            if (depth == 0)
            {
                return Evaluation.Heuristic(state);
            }

            int alpha = beta - 1;
            TtEntry tte;
            Move pvMove = default;

            var ttRef = tt.TryGetReference(state.Hash);
            if (ttRef != null)
            {
                tte = ttRef.Value;
                if (tte.Depth >= depth)
                {
                    // beta-cutoff
                    if (tte.LowerBound >= beta)
                    {
                        tt.Touch(ttRef);
                        return tte.LowerBound;
                    }
                    // alpha-cutoff
                    if (tte.UpperBound <= alpha)
                    {
                        tt.Touch(ttRef);
                        return tte.UpperBound;
                    }
                    // we know the exact value
                    if (tte.LowerBound == tte.UpperBound)
                    {
                        tt.Touch(ttRef);
                        return tte.LowerBound;
                    }

                    // use the information to refine our bounds
                    // todo: this may not actually work (see note below)
                    alpha = Math.Max(alpha, tte.LowerBound);
                    beta = Math.Min(beta, tte.UpperBound);
                }
                pvMove = ttRef.Value.PvMove;
                Debug.Assert(!pvMove.IsDefault);
            }

            int guess = state.WhiteToMove ? int.MinValue : int.MaxValue;
            var childKillers = KillerMoves.Empty;
            bool pvCausesCut = false;
            if (!pvMove.IsDefault)
            {
                bool success = state.TryApply(pvMove, out _);
                Debug.Assert(success);
                guess = NullWindowSearch(state, beta, depth - 1, tt, ref childKillers);
                state.Undo();
                if (state.WhiteToMove)
                {
                    pvCausesCut = (guess >= beta);
                    if (pvCausesCut)
                    {
                        Log.Debug("PV move {0} caused beta cutoff for state {1} with guess={2} beta={3}", pvMove, state, guess, beta);
                        killers = killers.Add(pvMove);
                    }
                }
                else
                {
                    pvCausesCut = (guess <= alpha);
                    if (pvCausesCut)
                    {
                        Log.Debug("PV move {0} caused alpha cutoff for state {1} with guess={2} alpha={3}", pvMove, state, guess, alpha);
                        killers = killers.Add(pvMove);
                    }
                }
            }

            if (!pvCausesCut)
            {
                int childrenSearched = 0;
                Move bestMove = default;

                Log.Debug("Commencing search of children of state {0}", state);
                Log.IndentLevel++;
                if (state.WhiteToMove)
                {
                    foreach (var move in state.GetPseudoLegalMoves(killers))
                    {
                        if (move == pvMove || !state.TryApply(move, out _)) continue;

                        childrenSearched++;

                        int value = NullWindowSearch(state, beta, depth - 1, tt, ref childKillers);
                        state.Undo();
                        bool better = value > guess;
                        if (better)
                        {
                            guess = value;
                            bestMove = move;

                            if (guess >= beta)
                            {
                                Log.Debug("Beta cutoff occurred with guess={0} beta={1} move={2}", guess, beta, move);
                                killers = killers.Add(move);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var move in state.GetPseudoLegalMoves(killers))
                    {
                        if (move == pvMove || !state.TryApply(move, out _)) continue;

                        childrenSearched++;

                        int value = NullWindowSearch(state, beta, depth - 1, tt, ref childKillers);
                        state.Undo();
                        bool better = value < guess;
                        if (better)
                        {
                            guess = value;
                            bestMove = move;

                            if (guess <= alpha)
                            {
                                Log.Debug("Alpha cutoff occurred with guess={0} alpha={1} move={2}", guess, alpha, move);
                                killers = killers.Add(move);
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

                pvMove = bestMove;
            }

            Debug.Assert(!pvMove.IsDefault);
            if (guess <= alpha) // fail-low result => upper bound
            {
                tte = new TtEntry(lowerBound: int.MinValue, upperBound: guess, depth, pvMove);
            }
            else // fail-high result => lower bound
            {
                Debug.Assert(guess >= beta); // must be true for null-window searches, where alpha == (beta - 1)
                tte = new TtEntry(lowerBound: guess, upperBound: int.MaxValue, depth, pvMove);
            }
            /*
            else // neither => this is the exact minimax value
            {
                Debug.Assert(guess > alpha && guess < beta);
                tte = new TtEntry(lowerBound: guess, upperBound: guess);
            }
            */

            int ttDepth = ttRef?.Value.Depth ?? 0;
            if (depth >= ttDepth) // information about higher depths is more valuable
            {
                if (depth == ttDepth)
                {
                    bool overlaps = (tte.LowerBound <= ttRef.Value.UpperBound) && (ttRef.Value.LowerBound <= tte.UpperBound);
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
                            lowerBound: Math.Max(tte.LowerBound, ttRef.Value.LowerBound),
                            upperBound: Math.Min(tte.UpperBound, ttRef.Value.UpperBound),
                            depth,
                            pvMove);
                    }
                }
                tt.UpdateOrAdd(ttRef, state.Hash, tte);
            }
            return guess;
        }
    }
}
