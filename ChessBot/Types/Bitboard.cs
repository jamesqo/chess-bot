using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    /// <summary>
    /// Stores a bit for each tile of a chess board.
    /// </summary>
    public readonly struct Bitboard : IEquatable<Bitboard>
    {
        private static readonly int[] MultiplyDeBruijnBitPosition = new int[64]
        {
            0, 1, 2, 53, 3, 7, 54, 27, 4, 38, 41, 8, 34, 55, 48, 28,
            62, 5, 39, 46, 44, 42, 22, 9, 24, 35, 59, 56, 49, 18, 29, 11,
            63, 52, 6, 26, 37, 40, 33, 47, 61, 45, 43, 21, 23, 58, 17, 10,
            51, 25, 36, 32, 60, 20, 57, 16, 50, 31, 19, 15, 30, 14, 13, 12
        };

        public static readonly Bitboard Zero = 0UL;
        public static readonly Bitboard AllOnes = ulong.MaxValue;

        public static implicit operator ulong(Bitboard bb) => bb._value;
        public static implicit operator Bitboard(ulong value) => new Bitboard(value);

        public static bool operator ==(Bitboard left, Bitboard right) => left.Equals(right);
        public static bool operator !=(Bitboard left, Bitboard right) => !(left == right);
        public static Bitboard operator &(Bitboard left, Bitboard right) => new Bitboard(left._value & right._value);
        public static Bitboard operator |(Bitboard left, Bitboard right) => new Bitboard(left._value | right._value);

        private readonly ulong _value;

        public Bitboard(ulong value) => _value = value;

        public bool IsZero => _value == 0;

        public bool this[Location location]
        {
            get
            {
                Debug.Assert(location.IsValid);
                return (_value & location.GetMask()) != 0;
            }
        }

        public Bitboard Clear(Location location)
        {
            unchecked
            {
                Debug.Assert(location.IsValid);
                return new Bitboard(_value & ~location.GetMask());
            }
        }

        public Bitboard ClearNext() // clears the lsb
        {
            unchecked
            {
                Debug.Assert(_value != 0);
                return new Bitboard(_value & (_value - 1));
            }
        }

        public int CountSetBits() // popcount
        {
            unchecked
            {
                ulong v = _value;
                v = v - ((v >> 1) & 0x5555555555555555UL);
                v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
                return (int)((((v + (v >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
            }
        }

        public Location NextLocation() => new Location((byte)IndexOfNext());

        public Bitboard Reverse()
        {
            unchecked
            {
                ulong v = _value;
                v = ((v >> 1) & 0x5555555555555555UL) | ((v & 0x5555555555555555UL) << 1);
                v = ((v >> 2) & 0x3333333333333333UL) | ((v & 0x3333333333333333UL) << 2);
                v = ((v >> 4) & 0x0F0F0F0F0F0F0F0FUL) | ((v & 0x0F0F0F0F0F0F0F0FUL) << 4);
                v = ((v >> 8) & 0x00FF00FF00FF00FFUL) | ((v & 0x00FF00FF00FF00FFUL) << 8);
                v = ((v >> 16) & 0x0000FFFF0000FFFFUL) | ((v & 0x0000FFFF0000FFFFUL) << 16);
                v = ((v >> 32) & 0x00000000FFFFFFFFUL) | ((v & 0x00000000FFFFFFFFUL) << 32);
                return v;
            }
        }

        public Bitboard Set(Location location)
        {
            unchecked
            {
                Debug.Assert(location.IsValid);
                return new Bitboard(_value | location.GetMask());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOfNext() // gets the index of the lsb
        {
            unchecked
            {
                Debug.Assert(_value != 0);
                return MultiplyDeBruijnBitPosition[((_value & (ulong)(-(long)_value)) * 0x022FDD63CC95386DUL) >> 58];
            }
        }

        public override bool Equals(object obj) => obj is Bitboard other && Equals(other);

        public bool Equals(Bitboard other) => _value == other._value;

        public override int GetHashCode() => _value.GetHashCode();

        public override string ToString() => Convert.ToString(unchecked((long)_value), 2).PadLeft(64, '0');

        internal static Builder CreateBuilder(Bitboard value = default) => new Builder(value);

        // todo: remove this
        /// <summary>
        /// Mutable struct that helps with generating <see cref="Bitboard"/> values.
        /// </summary>
        /// <remarks>
        /// Since this is a mutable struct, try not to copy it or you may get unintuitive behavior.
        /// </remarks>
        internal struct Builder
        {
            private Bitboard _value;

            public Builder(Bitboard value) => _value = value;

            public Bitboard Value => _value;

            public void Clear(Location location) => _value = _value.Clear(location);
            public void ClearRange(Bitboard bb) => _value &= ~bb;
            public void Set(Location location) => _value = _value.Set(location);
            public void SetRange(Bitboard bb) => _value |= bb;
        }
    }
}
