using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses MTD-f search to pick the best move.
    /// </summary>
    public class MtdfPicker : IMovePicker<MtdfPicker.Info>
    {
        public class Info
        {
            internal Info(int utility) => Utility = utility;

            public int Utility { get; }
        }

        private readonly struct TtEntry
        {
            public TtEntry(int lowerBound, int upperBound)
            {
                Debug.Assert(lowerBound <= upperBound);

                LowerBound = lowerBound;
                UpperBound = upperBound;
            }

            public int LowerBound { get; }
            public int UpperBound { get; }

            public override string ToString() => $"[{LowerBound}, {UpperBound}]";
        }

        private readonly TranspositionTable<TtEntry> _tt;

        public MtdfPicker(int depth)
        {
            Depth = depth;
            _tt = new TranspositionTable<TtEntry>();
        }

        public int Depth { get; set; }
        public int FirstGuess { get; set; } = 0;

        public Move PickMove(State state) => PickMove(state, out _);

        public Move PickMove(State state, out Info info)
        {
            _tt.Clear(); // in case Depth changed

            Move bestMove = default;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;

            foreach (var (move, succ) in state.GetSuccessors())
            {
                isTerminal = false;

                int value = Mtdf(succ, FirstGuess, Depth - 1, _tt, bestValue);
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

        private static int Mtdf(
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
            if (tt.TryGetNode(state, out var ttNode))
            {
                tte = ttNode.Value;
                if (tte.LowerBound >= beta) return tte.LowerBound; // beta-cutoff
                if (tte.UpperBound <= alpha) return tte.UpperBound; // alpha-cutoff
                //if (tte.LowerBound == tte.UpperBound) return tte.LowerBound; // we know the exact value

                // use the information to refine our bounds
                alpha = Math.Max(alpha, tte.LowerBound);
                beta = Math.Min(beta, tte.UpperBound);
            }

            var succs = state.GetSuccessors();
            bool isTerminal = true;

            int guess;
            if (state.WhiteToMove)
            {
                guess = int.MinValue;
                int a = alpha;
                foreach (var (_, succ) in succs)
                {
                    isTerminal = false;

                    if (guess >= beta) break;

                    guess = Math.Max(guess, AlphaBetaWithMemory(succ, a, beta, depth - 1, tt));
                    a = Math.Max(a, guess);
                }
            }
            else
            {
                guess = int.MaxValue;
                int b = beta;
                foreach (var (_, succ) in succs)
                {
                    isTerminal = false;

                    if (guess <= alpha) break;

                    guess = Math.Min(guess, AlphaBetaWithMemory(succ, alpha, b, depth - 1, tt));
                    b = Math.Min(b, guess);
                }
            }

            if (isTerminal)
            {
                return Evaluation.Terminal(state);
            }

            if (guess <= alpha) // fail-low result => upper bound
            {
                tte = new TtEntry(lowerBound: int.MinValue, upperBound: guess);
            }
            else // fail-high result => lower bound
            {
                // for now, we're only calling this with null-window searches where alpha == beta - 1
                Debug.Assert(guess >= beta);
                tte = new TtEntry(lowerBound: guess, upperBound: int.MaxValue);
            }
            /*
            else // neither => this is the exact minimax value
            {
                Debug.Assert(guess > alpha && guess < beta);
                tte = new TtEntry(lowerBound: guess, upperBound: guess);
            }
            */

            if (ttNode != null)
            {
                // improve on what we already know
                tte = new TtEntry(
                    Math.Max(tte.LowerBound, ttNode.Value.LowerBound),
                    Math.Min(tte.UpperBound, ttNode.Value.UpperBound));
                ttNode.Value = tte;
            }
            else
            {
                tt.Add(state, tte);
            }

            return guess;
        }
    }
}
