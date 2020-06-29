using ChessBot.Helpers;
using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;

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

        private readonly Stopwatch _sw;

        public Mtdf()
        {
            _sw = new Stopwatch();
        }

        public string Name => "mtdf";

        public int FirstGuess { get; set; } = 0;
        public int Depth { get; set; } = 0;
        public int MaxNodes { get; set; } = int.MaxValue;
        public ITranspositionTable Tt { get; set; }
        public int QuiescenceDepth { get; set; } = -5;

        private ITranspositionTable<TtEntry> _tt;
        private CancellationToken _ct;
        private int _nodesSearched;
        private int _nodesRemaining;
        private PvTable _pvTable;

        public override string ToString() => $"{Name} firstGuess={FirstGuess} depth={Depth} maxNodes={MaxNodes}";

        public ITranspositionTable MakeTt(int capacity)
        {
            return new TwoTierReplacementTt<TtEntry>(capacity);
        }

        public ISearchInfo Search(State root, CancellationToken cancellationToken = default)
        {
            if (Depth <= 0)
            {
                throw new InvalidOperationException($"{nameof(Depth)} wasn't set");
            }

            if (MaxNodes <= 0)
            {
                throw new InvalidOperationException($"Bad value for {nameof(MaxNodes)}");
            }

            if (!(Tt is ITranspositionTable<TtEntry> tt))
            {
                throw new InvalidOperationException($"Bad value for {nameof(Tt)}");
            }

            Log.Debug("Starting MTD-f search of {0} with f={1} d={2} maxNodes={3}", root, FirstGuess, Depth, MaxNodes);
            _sw.Restart();
            int score = RunMtdf(root.ToMutable(), cancellationToken, out var pv);
            _sw.Stop();
            var elapsed = _sw.Elapsed;
            Log.Debug("Finished MTD-f search of {0} with f={1} d={2} maxNodes={3}", root, FirstGuess, Depth, MaxNodes);
            Log.Debug(
                "Got score={0} pv={1}, searched {2} nodes in {3} ms",
                score, string.Join(' ', pv), _nodesSearched, (int)elapsed.TotalMilliseconds);

            if (pv.IsDefault) Debugger.Break();
            return new SearchInfo(Depth, elapsed, _nodesSearched, pv, score);
        }

        private int RunMtdf(MutState root, CancellationToken ct, out ImmutableArray<Move> pv)
        {
            _tt = (ITranspositionTable<TtEntry>)Tt;
            _ct = ct;
            _nodesSearched = 0;
            _nodesRemaining = MaxNodes;
            _pvTable = new PvTable(Depth); // todo: incorporate into SearchStack?

            int guess = FirstGuess;
            var (lowerBound, upperBound) = (Evaluation.MinScore, Evaluation.MaxScore);

            do
            {
                int beta = guess == lowerBound ? (guess + 1) : guess;
                Log.Debug("Starting null-window search for state {0} with beta={1}", root, beta);

                // todo: incorporate into SearchStack?
                var unused = Killers.Empty;
                guess = NullWindowSearch(root, beta, Depth, ref unused);
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
            while (lowerBound < upperBound && _nodesRemaining > 0);

            pv = _pvTable.GetTop().ToImmutableArray();
            if (pv.IsDefault) Debugger.Break();
            return guess;
        }

        // does alpha-beta search on the null window [beta-1, beta] using a transposition table.
        // for maintainability purposes, the score returned is from the active player's perspective.
        private int NullWindowSearch(
            MutState state,
            int beta,
            int depth,
            ref Killers killers)
        {
            Debug.Assert(beta != int.MinValue); // doesn't negate properly
            Debug.Assert(depth >= 0);
            Debug.Assert(_nodesRemaining > 0);

            int alpha = beta - 1; // null window search
            _nodesRemaining--;
            _nodesSearched++;

            // Unfortunately, it's pretty expensive to check for mate/stalemate since it involves trying to enumerate the current state's successors.
            // As a result, we don't bother checking for those conditions and returning the correct value when depth == 0.

            if (depth == 0)
            {
                // todo: quiescence search
                return state.Heuristic;
            }

            // Check for cancellation

            if (_nodesRemaining == 0 || _ct.IsCancellationRequested)
            {
                _pvTable.SetNone(depth);
                return state.Heuristic;
            }

            // TT lookup

            TtEntry tte;
            Move storedPvMove = default;

            var ttRef = _tt.TryGetReference(state.Hash);
            if (ttRef != null)
            {
                tte = ttRef.Value;
                storedPvMove = tte.PvMove;

                if (tte.Depth >= depth)
                {
                    // beta-cutoff / fail-high
                    if (tte.LowerBound >= beta)
                    {
                        _tt.Touch(ttRef);
                        _pvTable.SetOne(depth, storedPvMove);
                        return tte.LowerBound;
                    }
                    // fail-low
                    if (tte.UpperBound <= alpha)
                    {
                        _tt.Touch(ttRef);
                        _pvTable.SetNone(depth);
                        return tte.UpperBound;
                    }
                    // we know the exact value
                    if (tte.LowerBound == tte.UpperBound)
                    {
                        _tt.Touch(ttRef);
                        _pvTable.SetOne(depth, storedPvMove);
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

                guess = -NullWindowSearch(state, -alpha, depth - 1, ref childKillers);
                state.Undo();

                _pvTable.BubbleUpTo(depth, storedPvMove);

                pvCausedCut = (guess >= beta || _nodesRemaining <= 0 || _ct.IsCancellationRequested);
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
                bool isTerminal = storedPvMove.IsDefault;

                Log.Debug("Commencing search of children of state {0}", state);
                Log.IndentLevel++;
                foreach (var move in state.GetPseudoLegalMoves(killers))
                {
                    if (move == storedPvMove || !state.TryApply(move, out _)) continue;
                    isTerminal = false;

                    int value = -NullWindowSearch(state, -alpha, depth - 1, ref childKillers);
                    state.Undo();

                    bool better = value > guess;
                    if (better)
                    {
                        guess = value;
                        pvMove = move;
                        _pvTable.BubbleUpTo(depth, move);
                    }

                    if (guess >= beta || _nodesRemaining <= 0 || _ct.IsCancellationRequested)
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

                if (isTerminal)
                {
                    _pvTable.SetNone(depth);
                    return Evaluation.OfTerminal(state);
                }
                Log.Debug("Finished search of children of state {0}", state);
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
                _tt.UpdateOrAdd(ttRef, state.Hash, tte);
            }
            return guess;
        }
    }
}
