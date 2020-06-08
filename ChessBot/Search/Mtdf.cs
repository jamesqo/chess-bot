using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Linq;

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

        private readonly struct TtEntry
        {
            public TtEntry(int lowerBound, int upperBound, int depth, Move firstMove)
            {
                Debug.Assert(lowerBound <= upperBound);
                Debug.Assert(depth > 0);
                Debug.Assert(firstMove.IsValid);

                LowerBound = lowerBound;
                UpperBound = upperBound;
                Depth = depth;
                FirstMove = firstMove;
            }

            public int LowerBound { get; }
            public int UpperBound { get; }
            public int Depth { get; }
            public Move FirstMove { get; }

            public override string ToString() => $"[{LowerBound}, {UpperBound}], {nameof(Depth)} = {Depth}";
        }

        private readonly TranspositionTable<TtEntry> _tt;

        public Mtdf(int depth)
        {
            Depth = depth;
            _tt = new TranspositionTable<TtEntry>();
        }

        public int Depth { get; set; }
        public int FirstGuess { get; set; } = 0;

        public Move PickMove(State state, out Info info)
        {
            Move bestMove = default;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;

            foreach (var (move, succ) in state.GetSuccessors())
            {
                isTerminal = false;

                int value = _Mtdf(succ, FirstGuess, Depth - 1, _tt, bestValue);
                bool better = (state.WhiteToMove ? value > bestValue : value < bestValue);
                if (better)
                {
                    bestValue = value;
                    bestMove = move;
                }
            }

            if (isTerminal)
            {
                throw new ArgumentException($"A terminal state was passed to {nameof(PickMove)}", nameof(state));
            }

            info = new Info(utility: bestValue);
            return bestMove;
        }

        private static int _Mtdf(
            State root,
            int firstGuess,
            int depth,
            TranspositionTable<TtEntry> tt,
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
                guess = AlphaBetaWithMemory(root, beta - 1, beta, depth, tt);
                if (guess < beta) // alpha-cutoff
                {
                    upperBound = guess;
                }
                else // beta-cutoff
                {
                    lowerBound = guess;
                }
            }
            while (lowerBound < upperBound);

            return guess;
        }

        private static int AlphaBetaWithMemory(
            State state,
            int alpha,
            int beta,
            int depth,
            TranspositionTable<TtEntry> tt)
        {
            Debug.Assert(alpha < beta);

            if (depth == 0)
            {
                return Evaluation.Heuristic(state);
            }

            TtEntry tte;
            Move firstMove = default;
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
                firstMove = ttNode.Value.FirstMove;
                Debug.Assert(!firstMove.IsDefault);
            }

            int guess = state.WhiteToMove ? int.MinValue : int.MaxValue;
            var (a, b) = (alpha, beta);
            bool firstMakesCut = false;
            if (!firstMove.IsDefault)
            {
                var succ = state.Apply(firstMove);
                guess = AlphaBetaWithMemory(succ, alpha, beta, depth - 1, tt);
                if (state.WhiteToMove)
                {
                    firstMakesCut = (guess >= beta);
                    a = Math.Max(a, guess);
                }
                else
                {
                    firstMakesCut = (guess <= alpha);
                    b = Math.Min(b, guess);
                }
            }

            if (!firstMakesCut)
            {
                var succs = state.GetSuccessors();
                bool isTerminal = true;

                if (state.WhiteToMove)
                {
                    foreach (var (move, succ) in succs)
                    {
                        isTerminal = false;

                        int value = AlphaBetaWithMemory(succ, a, beta, depth - 1, tt);
                        bool better = value > guess;
                        if (better)
                        {
                            guess = value;
                            firstMove = move;
                            a = Math.Max(a, guess);

                            if (guess >= beta) break;
                        }
                    }
                }
                else
                {
                    foreach (var (move, succ) in succs)
                    {
                        isTerminal = false;

                        int value = AlphaBetaWithMemory(succ, alpha, b, depth - 1, tt);
                        bool better = value < guess;
                        if (better)
                        {
                            guess = value;
                            firstMove = move;
                            b = Math.Min(b, guess);

                            if (guess <= alpha) break;
                        }
                    }
                }

                if (isTerminal)
                {
                    return Evaluation.Terminal(state);
                }
            }

            Debug.Assert(!firstMove.IsDefault);
            if (guess <= alpha) // fail-low result => upper bound
            {
                tte = new TtEntry(lowerBound: int.MinValue, upperBound: guess, depth: depth, firstMove: firstMove);
            }
            else // fail-high result => lower bound
            {
                // for now, we're only calling this with null-window searches where alpha == beta - 1
                Debug.Assert(guess >= beta);
                tte = new TtEntry(lowerBound: guess, upperBound: int.MaxValue, depth: depth, firstMove: firstMove);
            }
            /*
            else // neither => this is the exact minimax value
            {
                Debug.Assert(guess > alpha && guess < beta);
                tte = new TtEntry(lowerBound: guess, upperBound: guess);
            }
            */

            if (ttNode != null && !ttNode.WasEvicted) // the node could have been evicted during a recursive call
            {
                int ttDepth = ttNode.Value.Depth;
                if (depth >= ttDepth) // information about higher depths is more valuable
                {
                    /*
                    if (depth == ttDepth)
                    {
                        // improve on what we already know
                        tte = new TtEntry(
                            Math.Max(tte.LowerBound, ttNode.Value.LowerBound),
                            Math.Min(tte.UpperBound, ttNode.Value.UpperBound),
                            depth: depth);
                    }
                    */
                    // ^ this looks reasonable but it doesn't work.
                    // suppose our utility is computed based off of the utility of a child with depth = x. later, the child gets a tt entry with an
                    // associated depth = y. the child entry could differ greatly from its earlier value, which could affect our minimax value (and
                    // make the earlier bounds obsolete) even though we're passing the same depth both times.
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
