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
                if (LowerBound != Evaluation.MinScore) sb.Append(LowerBound);
                sb.Append(", ");
                if (UpperBound != Evaluation.MaxScore) sb.Append(UpperBound);
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

        // Search parameters
        public int FirstGuess { get; set; } = 0;
        public int Depth { get; set; } = 0;
        public int MaxNodes { get; set; } = int.MaxValue;
        public ITranspositionTable Tt { get; set; }
        public int QuiescenceDepth { get; set; } = -5;

        // Search state variables
        private ITranspositionTable<TtEntry> _tt;
        private CancellationToken _ct;
        private int _nodesSearched;
        private int _nodesRemaining;
        private PvTable _pvt;
        private KillerTable _kt;

        public override string ToString() => $"{Name} firstGuess={FirstGuess} depth={Depth} maxNodes={MaxNodes}";

        public ITranspositionTable MakeTt(int capacity)
        {
            return new TwoTierReplacementTt<TtEntry>(capacity);
        }

        #region Search implementation

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

            if (!(Tt is ITranspositionTable<TtEntry>))
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

            return new SearchInfo(Depth, elapsed, _nodesSearched, pv, score);
        }

        private int RunMtdf(MutState root, CancellationToken ct, out ImmutableArray<Move> pv)
        {
            _tt = (ITranspositionTable<TtEntry>)Tt;
            _ct = ct;
            _nodesSearched = 0;
            _nodesRemaining = MaxNodes;
            _pvt = new PvTable(Depth);
            _kt = new KillerTable(Depth);

            int guess = FirstGuess;
            var (lowerBound, upperBound) = (Evaluation.MinScore, Evaluation.MaxScore);

            do
            {
                int beta = guess == lowerBound ? (guess + 1) : guess;
                Log.Debug("Starting null-window search for state {0} with beta={1}", root, beta);

                guess = NullWindowSearch(root, beta, Depth);
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

            pv = _pvt.GetTopPv().ToImmutableArray();
            return guess;
        }

        // does alpha-beta search on the null window [beta-1, beta] using a transposition table.
        // the score returned is from the active player's perspective.
        private int NullWindowSearch(MutState state, int beta, int depth)
        {
            Debug.Assert(depth >= 0 && depth <= Depth);
            Debug.Assert(_nodesRemaining > 0);

            int alpha = beta - 1; // null window search
            _nodesRemaining--;
            _nodesSearched++;

            // Unfortunately, it's pretty expensive to check for mate/stalemate since it involves trying to enumerate the current state's successors.
            // As a result, we don't bother checking for those conditions and returning the correct value when depth == 0.

            if (depth == 0)
            {
                return Quiesce(state, alpha, beta, depth: 0);
            }

            // Check for cancellation

            if (Canceled)
            {
                _pvt.SetNoPv(depth);
                return state.Heuristic;
            }

            // TT lookup

            TtEntry tte;
            Move storedPvMove = default;

            if (TtLookup(state, alpha, beta, depth, out var ttRef, out int bound))
            {
                return bound;
            }
            else if (ttRef != null)
            {
                tte = ttRef.Value;
                storedPvMove = tte.PvMove;

                if (tte.Depth >= depth)
                {
                    // use the information to refine our bounds
                    // note: this may not actually work? (see below comment about outdated TT entries)
                    alpha = Math.Max(alpha, tte.LowerBound);
                    beta = Math.Min(beta, tte.UpperBound);
                }
            }

            // Search the PV move first if we already stored one for this node

            int guess = Evaluation.MinScore;
            bool pvCausedCut = false;

            if (!storedPvMove.IsDefault)
            {
                Log.IndentLevel++;
                bool success = state.TryApply(storedPvMove, out _);
                Debug.Assert(success);

                guess = -NullWindowSearch(state, -alpha, depth - 1);
                state.Undo();

                _pvt.BubbleUp(depth, storedPvMove);

                pvCausedCut = (guess >= beta || Canceled);
                if (guess >= beta)
                {
                    Log.Debug("Beta cutoff occurred for state {0} with guess={1} beta={2} storedPvMove={3}", state, guess, beta, storedPvMove);
                    if (!state.IsCapture(storedPvMove)) _kt.Add(depth, storedPvMove);
                }
                Log.IndentLevel--;
            }

            // If we haven't stored a PV move or it failed to produce a cutoff, search the other moves

            Move pvMove = storedPvMove;

            if (!pvCausedCut)
            {
                bool isTerminal = storedPvMove.IsDefault;

                Log.Debug("Commencing search of children of state {0}", state);
                Log.IndentLevel++;

                if (depth > 1) _kt.Clear(depth - 1); // clear child killers
                var killers = _kt[depth];
                foreach (var move in state.GetPseudoLegalMoves(killers: killers))
                {
                    if (move == storedPvMove || !state.TryApply(move, out _)) continue;
                    isTerminal = false;

                    int value = -NullWindowSearch(state, -alpha, depth - 1);
                    state.Undo();

                    bool better = value > guess || pvMove.IsDefault; // in case we get the minimum possible score
                    if (better)
                    {
                        guess = value;
                        pvMove = move;
                        _pvt.BubbleUp(depth, pvMove);
                    }

                    if (guess >= beta || Canceled)
                    {
                        if (guess >= beta)
                        {
                            Log.Debug("Beta cutoff occurred for state {0} with guess={1} beta={2} pvMove={3}", state, guess, beta, pvMove);
                            if (!state.IsCapture(pvMove)) _kt.Add(depth, pvMove);
                        }
                        break;
                    }
                }

                Log.IndentLevel--;

                if (isTerminal)
                {
                    _pvt.SetNoPv(depth);
                    return Evaluation.OfTerminal(state);
                }
                Log.Debug("Finished search of children of state {0}", state);
            }

            // Store information from the search in TT

            TtStore(state, guess, alpha, beta, depth, pvMove, ttRef);

            // Postconditions

            Debug.Assert(!pvMove.IsDefault);
            CheckPv(state, _pvt.GetPv(depth));

            return guess;
        }

        // todo: make use of TT
        private int Quiesce(MutState state, int alpha, int beta, int depth)
        {
            Debug.Assert(alpha < beta);
            Debug.Assert(depth <= 0 && depth >= QuiescenceDepth);

            int guess = state.Heuristic;
            if (depth == QuiescenceDepth) return guess;

            // fail-soft lower bound based on null move observation: we assume that we're not in zugzwang
            // and that there is at least one move in the current position that would improve the heuristic.
            if (guess >= beta) return guess;
            alpha = Math.Max(alpha, guess);

            foreach (var capture in state.GetPseudoLegalMoves(flags: MoveFlags.Captures))
            {
                if (!state.TryApply(capture, out _)) continue;
                int value = -Quiesce(state, -beta, -alpha, depth - 1);
                state.Undo();

                guess = Math.Max(guess, value);
                if (guess >= beta) return guess;
                alpha = Math.Max(alpha, guess);
            }

            // fail-soft upper bound
            Debug.Assert(guess <= alpha);
            return guess;
        }

        #endregion

        #region Search helpers

        private bool Canceled
        {
            get
            {
                Debug.Assert(_nodesRemaining >= 0);
                return _nodesRemaining == 0 || _ct.IsCancellationRequested;
            }
        }

        private bool TtLookup(MutState state, int alpha, int beta, int depth, out ITtReference<TtEntry> ttRef, out int bound)
        {
            ttRef = _tt.TryGetReference(state.Hash);
            if (ttRef != null)
            {
                var tte = ttRef.Value;
                if (tte.Depth >= depth)
                {
                    // beta-cutoff / fail-high
                    if (tte.LowerBound >= beta)
                    {
                        _tt.Touch(ttRef);
                        _pvt.SetOneMovePv(depth, tte.PvMove);
                        bound = tte.LowerBound;
                        return true;
                    }
                    // fail-low
                    if (tte.UpperBound <= alpha)
                    {
                        _tt.Touch(ttRef);
                        _pvt.SetNoPv(depth);
                        bound = tte.UpperBound;
                        return true;
                    }
                    // we know the exact value
                    if (tte.LowerBound == tte.UpperBound)
                    {
                        _tt.Touch(ttRef);
                        _pvt.SetOneMovePv(depth, tte.PvMove);
                        bound = tte.LowerBound;
                        return true;
                    }
                }
            }

            bound = default;
            return false;
        }

        private void TtStore(MutState state, int guess, int alpha, int beta, int depth, Move pvMove, ITtReference<TtEntry> existingRef)
        {
            Debug.Assert(alpha < beta);
            Debug.Assert(depth > 0 && depth <= Depth);
            Debug.Assert(pvMove.IsValid);

            int lowerBound, upperBound;

            if (guess <= alpha) // fail-low => upper bound
            {
                // We don't store the "PV move" for alpha cutoffs. Since we've only been maximizing over upper bounds,
                // we only know that no move is good enough to produce a score greater than alpha; we can't tell which one is best.
                // Also, if the move failed low this time, there's little reason to expect it would cause a beta cutoff next time.

                pvMove = default;
                lowerBound = Evaluation.MinScore;
                upperBound = guess;
            }
            else // beta-cutoff, aka fail-high => lower bound
            {
                Debug.Assert(guess >= beta); // must be true for null-window searches, as alpha == beta - 1
                lowerBound = guess;
                upperBound = Evaluation.MaxScore;
            }

            if (existingRef == null || existingRef.HasExpired)
            {
                _tt.Add(state.Hash, new TtEntry(lowerBound, upperBound, depth, pvMove));
                return;
            }

            var tte = existingRef.Value;
            if (depth < tte.Depth) return; // information about higher depths is more valuable

            if (depth == tte.Depth)
            {
                bool overlaps = (lowerBound <= tte.UpperBound) && (tte.LowerBound <= upperBound);

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
                    if (pvMove.IsDefault) pvMove = tte.PvMove;
                    lowerBound = Math.Max(lowerBound, tte.LowerBound);
                    upperBound = Math.Max(upperBound, tte.UpperBound);
                }
            }

            bool updated = _tt.Update(existingRef, new TtEntry(lowerBound, upperBound, depth, pvMove));
            Debug.Assert(updated);
        }

        [Conditional("DEBUG")]
        private static void CheckPv(MutState state, Span<Move> pv)
        {
            foreach (var move in pv)
            {
                bool success = state.TryApply(move, out _);
                Debug.Assert(success);
            }

            for (int i = 0; i < pv.Length; i++) state.Undo();
        }

        #endregion
    }
}
