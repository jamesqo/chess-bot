using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    /// <summary>
    /// Represents a list of tiles on a chess board.
    /// </summary>
    public class Board : IEquatable<Board>
    {
        internal const int NumberOfTiles = 64;

        public static readonly Board Empty = new Board(0UL, 0UL, 0UL, 0UL);

        // these aren't readonly because they're modded by the Builder class
        private Bitboard _value1;
        private Bitboard _value2;
        private Bitboard _value3;
        private Bitboard _value4;

        public Board(Bitboard value1, Bitboard value2, Bitboard value3, Bitboard value4)
        {
            _value1 = value1;
            _value2 = value2;
            _value3 = value3;
            _value4 = value4;
        }

        // todo: should this be returning a Piece? instead?
        public Tile this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(location.IsValid);

                var bit1 = ((uint)(_value1 >> location.Value) & 1U);
                var bit2 = ((uint)(_value2 >> location.Value) & 1U);
                var bit3 = ((uint)(_value3 >> location.Value) & 1U);
                var bit4 = ((uint)(_value4 >> location.Value) & 1U);
                byte pieceValue = (byte)(bit1 | (bit2 << 1) | (bit3 << 2) | (bit4 << 3));
                return new Tile((ushort)(location.Value | (pieceValue << Location.NumberOfBits)));
            }
        }

        public override bool Equals(object obj) => Equals(obj as Board);

        public bool Equals([AllowNull] Board other)
        {
            if (other == null) return false;
            return _value1 == other._value1
                && _value2 == other._value2
                && _value3 == other._value3
                && _value4 == other._value4;
        }

        public override int GetHashCode() => HashCode.Combine(_value1, _value2, _value3, _value4);

        public TilesEnumerator GetTiles() => new TilesEnumerator(this);

        public OccupiedTilesEnumerator GetOccupiedTiles() => new OccupiedTilesEnumerator(this);

        internal static Builder CreateBuilder(Board value = null) => new Builder(value ?? Empty);

        public struct TilesEnumerator
        {
            private readonly Board _board;
            private int _location;

            internal TilesEnumerator(Board board)
            {
                _board = board;
                _location = -1;
            }

            public Tile Current => _board[new Location((byte)_location)];

            public TilesEnumerator GetEnumerator() => this;

            public bool MoveNext() => ++_location < 64;
        }

        public struct OccupiedTilesEnumerator
        {
            private Board _board;
            private int _location;
            private Tile _current;

            internal OccupiedTilesEnumerator(Board board)
            {
                _board = board;
                _location = -1;
                _current = default;
            }

            public Tile Current => _current;

            public OccupiedTilesEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                Debug.Assert(_location < 64);

                while (true)
                {
                    if (++_location >= 64) return false;
                    _current = _board[new Location((byte)_location)];
                    if (_current.HasPiece) return true;
                }
            }
        }

        /// <summary>
        /// Mutable struct that helps with generating <see cref="Types.Board"/> values.
        /// </summary>
        /// <remarks>
        /// Since this is a mutable struct, try not to copy it or you may get unintuitive behavior.
        /// Also, DO NOT use any of the mutating methods after getting <see cref="Value"/>! It may violate
        /// <see cref="Types.Board"/>'s immutability contract. Unfortunately, we have no way of enforcing this
        /// since this type is a struct for performance reasons.
        /// </remarks>
        internal struct Builder
        {
            private readonly Board _value;

            // We make a copy since the inner Board object is modified directly
            public Builder(Board value) => _value = Copy(value);

            public Board Value => _value;

            public void Set(Location location, Piece? piece)
            {
                Debug.Assert(_value != null);
                Debug.Assert(location.IsValid);

                var pieceValue = (piece?.Value + 1) ?? 0; // 0 represents an empty tile
                var mask = location.GetMask();
                ref Bitboard value1 = ref _value._value1,
                             value2 = ref _value._value2,
                             value3 = ref _value._value3,
                             value4 = ref _value._value4;

                if ((pieceValue & 1) != 0) value1 |= mask; else value1 &= ~mask;
                if ((pieceValue & 2) != 0) value2 |= mask; else value2 &= ~mask;
                if ((pieceValue & 4) != 0) value3 |= mask; else value3 &= ~mask;
                if ((pieceValue & 8) != 0) value4 |= mask; else value4 &= ~mask;
            }

            private static Board Copy(Board value)
            {
                return new Board(
                    value._value1,
                    value._value2,
                    value._value3,
                    value._value4);
            }
        }
    }
}
