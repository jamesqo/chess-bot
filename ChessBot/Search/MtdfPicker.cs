using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    public class MtdfPicker : IMovePicker
    {
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

        private readonly int _depth;
        private readonly int _firstGuess;
        private readonly TranspositionTable<TtEntry> _tt;

        public MtdfPicker(int depth, int firstGuess = 0)
        {
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));

            _depth = depth;
            _firstGuess = firstGuess;
            _tt = new TranspositionTable<TtEntry>();
        }

        public Move PickMove(State state)
        {
            Move bestMove = default;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
            bool isTerminal = true;

            foreach (var (move, succ) in state.GetSuccessors())
            {
                isTerminal = false;

                int value = Mtdf(succ, _firstGuess, _depth - 1, bestValue);
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

            return bestMove;
        }

        private int Mtdf(State root, int f, int d, int bestSiblingValue)
        {
            int guess = f;
            int lowerBound, upperBound;

            bool whiteToMoveInParent = !root.WhiteToMove;
            if (whiteToMoveInParent)
            {
                (lowerBound, upperBound) = (bestSiblingValue, int.MaxValue);
            }
            else
            {
                (lowerBound, upperBound) = (int.MinValue, bestSiblingValue);
            }

            do
            {
                int beta = guess == lowerBound ? (guess + 1) : guess;
                guess = AlphaBetaWithMemory(root, beta - 1, beta, d);
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

        private int AlphaBetaWithMemory(State state, int alpha, int beta, int d)
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

                    guess = Math.Max(guess, AlphaBetaWithMemory(succ, a, beta, d - 1));
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

                    guess = Math.Min(guess, AlphaBetaWithMemory(succ, alpha, b, d - 1));
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
                _tt.Add(state, tte);
            }

            return guess;
        }
    }
}
