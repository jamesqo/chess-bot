using ChessBot.Helpers;
using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses MTD-f search to pick the best move.
    /// </summary>
    public class Mtdf : ISearchAlgorithm
    {
        private readonly struct TtEntry : IHasDepth
        {
            public TtEntry(int lowerBound, int upperBound, int depth, Move pvMove)
            {
                Debug.Assert(lowerBound <= upperBound);
                Debug.Assert(depth > 0);

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
                sb.Append(", ");
                sb.Append(nameof(PvMove));
                sb.Append(" = ");
                sb.Append(PvMove.ToString());
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

        public string Name => "mtdf";

        public int Depth { get; set; } = 0;
        public int FirstGuess { get; set; } = 0;
        public int MaxNodes { get; set; } = int.MaxValue;

        public override string ToString() => $"{Name} depth={Depth} firstGuess={FirstGuess} maxNodes={MaxNodes}";

        public ISearchInfo Search(State root)
        {
            if (Depth <= 0)
            {
                throw new InvalidOperationException($"{nameof(Depth)} wasn't set");
            }

            if (MaxNodes <= 0)
            {
                throw new InvalidOperationException($"Bad value for {nameof(MaxNodes)}");
            }

            Log.Debug("Starting MTD-f search of {0} with f={1} d={2} maxNodes={3}", root, FirstGuess, Depth, MaxNodes);
            _sw.Restart();
            int score = RunMtdf(root.ToMutable(), FirstGuess, Depth, _tt, MaxNodes, out var pv, out int nodesSearched);
            _sw.Stop();
            var elapsed = _sw.Elapsed;
            Log.Debug("Finished MTD-f search of {0} with f={1} d={2} maxNodes={3}", root, FirstGuess, Depth, MaxNodes);
            Log.Debug(
                "Got score={0} pv={1}, searched {2} nodes in {3} ms",
                score, string.Join(' ', pv), nodesSearched, elapsed.TotalMilliseconds);

            return new SearchInfo(Depth, elapsed, nodesSearched, pv, score);
        }

        private static int RunMtdf(
            MutState root,
            int firstGuess,
            int depth,
            ITranspositionTable<TtEntry> tt,
            int maxNodes,
            out ImmutableArray<Move> pv,
            out int nodesSearched)
        {
            nodesSearched = 0;

            int guess = firstGuess;
            var (lowerBound, upperBound) = (Evaluation.MinScore, Evaluation.MaxScore);
            var pvTable = new PvTable(depth);

            do
            {
                int beta = guess == lowerBound ? (guess + 1) : guess;
                Log.Debug("Starting null-window search for state {0} with beta={1}", root, beta);

                var unused = Killers.Empty;
                guess = NullWindowSearch(root, beta, depth, tt, maxNodes, pvTable, out int nodesSearchedThisIteration, ref unused);
                nodesSearched += nodesSearchedThisIteration;
                maxNodes -= nodesSearchedThisIteration;
                Log.Debug("Null-window search for state {0} with beta={1} returned {2}", root, beta, guess);

                if (guess < beta) // the real value is <= guess
                {
                    upperBound = guess;
                }
                else // beta-cutoff: the real value is >= guess
                {
                    lowerBound = guess;
                }

                // EXPERIMENTAL: trying binary search
                guess = (int)(((long)lowerBound + (long)upperBound) / 2);
            }
            while (lowerBound < upperBound && maxNodes > 0);

            pv = pvTable.GetTop().ToImmutableArray();
            return guess;
        }

        // does alpha-beta search on the null window [beta-1, beta] using a transposition table.
        // for maintainability purposes, the score returned is from the active player's perspective.
        private static int NullWindowSearch(
            MutState state,
            int beta,
            int depth,
            ITranspositionTable<TtEntry> tt,
            int maxNodes,
            PvTable pvTable,
            out int nodesSearched,
            ref Killers killers)
        {
            Debug.Assert(beta != int.MinValue); // doesn't negate properly
            Debug.Assert(depth >= 0);
            Debug.Assert(maxNodes > 0);

            int alpha = beta - 1; // null window search
            maxNodes--;
            nodesSearched = 1;

            // Unfortunately, it's pretty expensive to check for mate/stalemate since it involves trying to enumerate the current state's successors.
            // As a result, we don't bother checking for those conditions and returning the correct value when depth == 0.

            if (depth == 0 || maxNodes == 0)
            {
                if (maxNodes == 0) pvTable.SetNone(depth);
                return Evaluation.Heuristic(state);
            }

            // TT lookup

            TtEntry tte;
            Move storedPvMove = default;

            var ttRef = tt.TryGetReference(state.Hash);
            if (ttRef != null)
            {
                tte = ttRef.Value;
                storedPvMove = tte.PvMove;

                if (tte.Depth >= depth)
                {
                    // beta-cutoff / fail-high
                    if (tte.LowerBound >= beta)
                    {
                        tt.Touch(ttRef);
                        pvTable.SetOne(depth, storedPvMove);
                        return tte.LowerBound;
                    }
                    // fail-low
                    if (tte.UpperBound <= alpha)
                    {
                        tt.Touch(ttRef);
                        pvTable.SetNone(depth);
                        return tte.UpperBound;
                    }
                    // we know the exact value
                    if (tte.LowerBound == tte.UpperBound)
                    {
                        tt.Touch(ttRef);
                        pvTable.SetOne(depth, storedPvMove);
                        return tte.LowerBound;
                    }

                    // use the information to refine our bounds
                    // note: this may not actually work? (see below comment about outdated TT entries)
                    alpha = Math.Max(alpha, tte.LowerBound);
                    beta = Math.Min(beta, tte.UpperBound);
                }
            }

            // Search the PV move first if we already stored one for this node

            int guess = Evaluation.MinScore;
            var childKillers = Killers.Empty;
            bool pvCausedCut = false;

            if (!storedPvMove.IsDefault)
            {
                bool success = state.TryApply(storedPvMove, out _);
                Debug.Assert(success);

                guess = -NullWindowSearch(state, -alpha, depth - 1, tt, maxNodes, pvTable, out int childrenSearched, ref childKillers);
                nodesSearched += childrenSearched;
                maxNodes -= childrenSearched;
                state.Undo();

                pvTable.BubbleUpTo(depth, storedPvMove);

                pvCausedCut = (guess >= beta || maxNodes <= 0);
                if (guess >= beta)
                {
                    Log.Debug("Beta cutoff occurred for state {0} with guess={1} beta={2} storedPvMove={3}", state, guess, beta, storedPvMove);
                    killers = killers.Add(storedPvMove);
                }
            }

            // If we haven't stored a PV move or it failed to produce a cutoff, search the other moves

            Move pvMove = storedPvMove;

            if (!pvCausedCut)
            {
                Log.Debug("Commencing search of children of state {0}", state);
                Log.IndentLevel++;
                foreach (var move in state.GetPseudoLegalMoves(killers))
                {
                    if (move == storedPvMove || !state.TryApply(move, out _)) continue;

                    int value = -NullWindowSearch(state, -alpha, depth - 1, tt, maxNodes, pvTable, out int childrenSearched, ref childKillers);
                    nodesSearched += childrenSearched;
                    maxNodes -= childrenSearched;
                    state.Undo();

                    bool better = value > guess;
                    if (better)
                    {
                        guess = value;
                        pvMove = move;
                        pvTable.BubbleUpTo(depth, move);
                    }

                    if (guess >= beta || maxNodes <= 0)
                    {
                        if (guess >= beta)
                        {
                            Log.Debug("Beta cutoff occurred for state {0} with guess={1} beta={2} pvMove={3}", state, guess, beta, pvMove);
                            killers = killers.Add(move);
                        }
                        break;
                    }
                }
                Log.IndentLevel--;

                if (nodesSearched == 1)
                {
                    pvTable.SetNone(depth);
                    return Evaluation.OfTerminal(state);
                }
                Log.Debug("Searched {0} nodes from state {1}", nodesSearched, state);
            }

            // Store information from the search in TT

            if (guess <= alpha) // fail-low => upper bound
            {
                // We don't store the "PV move" for alpha cutoffs. Since we've only been maximizing over upper bounds,
                // we only know that no move is good enough to produce a score greater than alpha; we can't tell which one is best.
                // Also, if the move caused an alpha cutoff this time, there's little reason to expect it would cause a beta cutoff next time.
                pvMove = default;
                tte = new TtEntry(lowerBound: Evaluation.MinScore, upperBound: guess, depth, pvMove);
            }
            else // beta-cutoff, aka fail-high => lower bound
            {
                Debug.Assert(guess >= beta); // must be true for null-window searches, as alpha == beta - 1
                tte = new TtEntry(lowerBound: guess, upperBound: Evaluation.MaxScore, depth, pvMove);
            }

            int ttDepth = ttRef?.Value.Depth ?? -1;
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
                        if (pvMove.IsDefault) pvMove = storedPvMove;
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
