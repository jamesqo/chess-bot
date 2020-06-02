using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    internal class TileList
    {
        private const int MaxTiles = 64;

        private readonly Bitboard _value1;
        private readonly Bitboard _value2;
        private readonly Bitboard _value3;
        private readonly Bitboard _value4;

        public TileList(Bitboard value1, Bitboard value2, Bitboard value3, Bitboard value4)
        {
            _value1 = value1;
            _value2 = value2;
            _value3 = value3;
            _value4 = value4;
        }

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

        public TileList Add(TileList other)
        {
            Debug.Assert(!OverlapsWith(other));
            return new TileList(
                _value1 | other._value1,
                _value2 | other._value2,
                _value3 | other._value3,
                _value4 | other._value4);
        }

        // perf can be improved greatly by using bitwise ANDs, but this is only being run in debug builds for now
        private bool OverlapsWith(TileList other)
        {
            for (int i = 0; i < MaxTiles; i++)
            {
                var location = new Location((byte)i);
                bool hasPiece = this[location].HasPiece;
                bool otherHasPiece = other[location].HasPiece;
                if (hasPiece && otherHasPiece) return true;
            }
            return false;
        }
    }
}
