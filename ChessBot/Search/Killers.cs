using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    public readonly struct Killers
    {
        private const int MaxCount = 2;

        public static readonly Killers Empty = default;

        private readonly Move _move0;
        private readonly Move _move1;

        private Killers(Move move0, Move move1)
        {
            _move0 = move0;
            _move1 = move1;
        }

        public int Count => Convert.ToInt32(!_move0.IsDefault) + Convert.ToInt32(!_move1.IsDefault);

        public Killers Add(Move move)
        {
            switch (Count)
            {
                case 0:
                    return new Killers(move, default);
                case 1:
                case MaxCount:
                    return new Killers(move, _move0);
                default:
                    Debug.Assert(false, $"Unrecognized count: {Count}");
                    return default;
            }
        }

        public bool Contains(Move move) => move == _move0 || move == _move1;

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private readonly Killers _killers;
            private int _position;

            internal Enumerator(Killers killers)
            {
                _killers = killers;
                _position = -1;
            }

            public Move Current => _position switch
            {
                0 => _killers._move0,
                1 => _killers._move1,
                _ => throw new InvalidOperationException()
            };

            public bool MoveNext()
            {
                int p = ++_position;
                return p >= 0 && p < _killers.Count;
            }
        }
    }
}
