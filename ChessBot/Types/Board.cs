using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ChessBot.Types
{
    /// <summary>
    /// Represents a list of tiles on a chess board.
    /// </summary>
    public class Board : IEquatable<Board>
    {
        private unsafe struct Buffer
        {
            public fixed byte Bytes[32];
        }

        internal const int NumberOfTiles = 64;

        public static readonly Board Empty = new Board();

        private Buffer _buffer;

        private Board()
        {
        }

        private Board(ref Buffer buffer)
        {
            _buffer = buffer;
        }

        public unsafe Piece? this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(location.IsValid);

                int index = location.Value;
                byte pieceValue = _buffer.Bytes[index / 2];
                if (index % 2 != 0) pieceValue >>= 4;
                pieceValue &= 0b00001111;
                return pieceValue == 0 ? (Piece?)null : new Piece((byte)(pieceValue - 1));
            }
        }

        public override bool Equals(object obj) => Equals(obj as Board);

        public bool Equals([AllowNull] Board other)
        {
            if (other == null) return false;
            return ToBytes().SequenceEqual(other.ToBytes());
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (byte b in ToBytes())
            {
                hc.Add(b);
            }
            return hc.ToHashCode();
        }

        public TilesEnumerator GetTiles() => new TilesEnumerator(this);

        public OccupiedTilesEnumerator GetOccupiedTiles() => new OccupiedTilesEnumerator(this);

        public unsafe byte[] ToBytes()
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                bytes[i] = _buffer.Bytes[i];
            }
            return bytes;
        }

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

            public Tile Current
            {
                get
                {
                    var loc = new Location((byte)_location);
                    return new Tile(loc, _board[loc]);
                }
            }

            public TilesEnumerator GetEnumerator() => this;

            public bool MoveNext() => ++_location < 64;
        }

        // todo: this can be made faster. simply OR all of the values together, then keep using IndexOfLsb().
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
                    var loc = new Location((byte)_location);
                    _current = new Tile(loc, _board[loc]);
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

            public unsafe Piece? this[Location location]
            {
                get => _value[location];
                set
                {
                    Debug.Assert(_value != null);
                    Debug.Assert(location.IsValid);
                    Debug.Assert(value?.IsValid ?? true);

                    int index = location.Value;
                    byte pieceValue = (byte)((value?.Value + 1) ?? 0); // 0 represents an empty tile
                    ref byte target = ref _value._buffer.Bytes[index / 2];
                    // todo: could use (index % 2) * 4 to avoid branching here?
                    if (index % 2 != 0)
                    {
                        pieceValue <<= 4;
                        target &= 0b00001111;
                    }
                    else
                    {
                        target &= 0b11110000;
                    }
                    target |= pieceValue;
                }
            }

            private static Board Copy(Board value)
            {
                return new Board(ref value._buffer);
            }
        }
    }
}
