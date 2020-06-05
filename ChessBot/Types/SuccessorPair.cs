using System;
using System.Diagnostics;

namespace ChessBot.Types
{
    public readonly struct SuccessorPair : IEquatable<SuccessorPair>
    {
        public static implicit operator SuccessorPair((Move, State) tuple)
        {
            return new SuccessorPair(tuple.Item1, tuple.Item2);
        }

        public SuccessorPair(Move move, State state)
        {
            Debug.Assert(move.IsValid);
            Debug.Assert(state != null);

            Move = move;
            State = state;
        }

        public Move Move { get; }
        public State State { get; }

        public void Deconstruct(out Move move, out State state)
        {
            move = Move;
            state = State;
        }

        public override bool Equals(object obj)
            => obj is SuccessorPair other && Equals(other);

        public bool Equals(SuccessorPair other)
            => Move.Equals(other.Move) && State.Equals(other.State);

        public override int GetHashCode() => HashCode.Combine(Move, State);
    }
}
