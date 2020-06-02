using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    internal class TileList
    {
        public readonly Bitboard Value1;
        public readonly Bitboard Value2;
        public readonly Bitboard Value3;
        public readonly Bitboard Value4;

        public TileList(Bitboard value1, Bitboard value2, Bitboard value3, Bitboard value4)
        {
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
            Value4 = value4;
        }

        public Tile this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(location.IsValid);

                var bit1 = ((uint)(Value1 >> location.Value) & 1U);
                var bit2 = ((uint)(Value2 >> location.Value) & 1U);
                var bit3 = ((uint)(Value3 >> location.Value) & 1U);
                var bit4 = ((uint)(Value4 >> location.Value) & 1U);
                byte pieceValue = (byte)(bit1 | (bit2 << 1) | (bit3 << 2) | (bit4 << 3));
                return new Tile((ushort)(location.Value | (pieceValue << Location.NumberOfBits)));
            }
        }
    }
}
