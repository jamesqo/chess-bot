using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Search
{
    public readonly struct KillerMoves
    {
        private const int MaxCount = 2;

        public static readonly KillerMoves Empty = default;

        private readonly Move _move0;
        private readonly Move _move1;

        private KillerMoves(Move move0, Move move1)
        {
            _move0 = move0;
            _move1 = move1;
        }

        public int Count => Convert.ToInt32(!_move0.IsDefault) + Convert.ToInt32(!_move1.IsDefault);

        public KillerMoves Add(Move move)
        {
            switch (Count)
            {
                case 0:
                    return new KillerMoves(move, default);
                case 1:
                case MaxCount:
                    return new KillerMoves(move, _move0);
                default:
                    Debug.Assert(false, $"Unrecognized count: {Count}");
                    return default;
            }
        }

        public bool Contains(Move move) => move.Equals(_move0) || move.Equals(_move1);

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private readonly KillerMoves _killers;
            private int _position;

            internal Enumerator(KillerMoves killers)
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
