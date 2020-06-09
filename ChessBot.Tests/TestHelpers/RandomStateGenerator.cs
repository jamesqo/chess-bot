using ChessBot.Types;
using System;
using System.Linq;

namespace ChessBot.Tests.TestHelpers
{
    internal class RandomStateGenerator
    {
        private readonly Random _rng;

        public RandomStateGenerator(int? seed = null)
        {
            _rng = seed.HasValue
                ? new Random(seed.Value)
                : new Random();
        }

        public State Next()
        {
            int numSteps = _rng.Next(1, 101);
            var result = State.Start;
            for (int i = 0; i < numSteps; i++)
            {
                var move = TryGetRandomMove(result);
                if (move is null) break; // terminal
                result = result.Apply(move.Value);
            }
            return result;
        }

        public State NextNonTerminal()
        {
            var result = Next();
            if (result.IsTerminal) result = result.Undo();
            return result;
        }

        private Move? TryGetRandomMove(State state)
        {
            var moves = state.GetMoves().ToArray();
            if (moves.Length == 0) return null;
            return moves[_rng.Next(0, moves.Length)];
        }
    }
}
