using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    // todo: cleanup the code here
    // todo: make this api usable since it's public (ie. add a parse method or something)
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

        private Board(in Buffer buffer)
        {
            _buffer = buffer;
        }

        internal unsafe PieceOrNone this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(location.IsValid);

                int index = location.Value;
                byte pieceValue = _buffer.Bytes[index / 2];
                pieceValue >>= (index % 2) * 4;
                pieceValue &= 0b0000_1111;
                return new PieceOrNone(pieceValue);
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

        public TileEnumerator GetTiles() => new TileEnumerator(this);

        public OccupiedTileEnumerator GetOccupiedTiles() => new OccupiedTileEnumerator(this);

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

        public struct TileEnumerator
        {
            private readonly Board _board;
            private int _location;

            internal TileEnumerator(Board board)
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

            public TileEnumerator GetEnumerator() => this;

            public bool MoveNext() => ++_location < 64;
        }

        // todo: this can be made faster. simply OR all of the values together, then keep using IndexOfLsb().
        public struct OccupiedTileEnumerator
        {
            private Board _board;
            private int _location;
            private Tile _current;

            internal OccupiedTileEnumerator(Board board)
            {
                _board = board;
                _location = -1;
                _current = default;
            }

            public Tile Current => _current;

            public OccupiedTileEnumerator GetEnumerator() => this;

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
        /// Mutable struct that helps with generating <see cref="Board"/> values.
        /// </summary>
        /// <remarks>
        /// Since this is a mutable struct, try not to copy it or you may get unintuitive behavior.
        /// Also, DO NOT use any of the mutating methods after getting <see cref="Value"/>! It may violate
        /// <see cref="Board"/>'s immutability contract. Unfortunately, we have no way of enforcing this
        /// since this type is a struct for performance reasons.
        /// </remarks>
        internal struct Builder
        {
            private readonly Board _value;

            // We make a copy since the inner Board object is modified directly
            public Builder(Board value) => _value = Copy(value);

            public Board Value => _value;

            public unsafe PieceOrNone this[Location location]
            {
                get => _value[location];
                set
                {
                    Debug.Assert(_value != null);
                    Debug.Assert(location.IsValid);
                    Debug.Assert(value.IsValid);

                    int index = location.Value;
                    byte pieceValue = value.Value; // 0 represents an empty tile
                    ref byte target = ref _value._buffer.Bytes[index / 2];
                    if (index % 2 != 0)
                    {
                        target &= 0b0000_1111;
                        target |= (byte)(pieceValue << 4);
                    }
                    else
                    {
                        target &= 0b1111_0000;
                        target |= pieceValue;
                    }
                }
            }

            private static Board Copy(Board value)
            {
                return new Board(in value._buffer);
            }
        }
    }
}
