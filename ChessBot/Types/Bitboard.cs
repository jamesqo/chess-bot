﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    public struct Bitboard : IEquatable<Bitboard>
    {
        private static readonly int[] MultiplyDeBruijnBitPosition = new int[64]
        {
            0, 1, 2, 53, 3, 7, 54, 27, 4, 38, 41, 8, 34, 55, 48, 28,
            62, 5, 39, 46, 44, 42, 22, 9, 24, 35, 59, 56, 49, 18, 29, 11,
            63, 52, 6, 26, 37, 40, 33, 47, 61, 45, 43, 21, 23, 58, 17, 10,
            51, 25, 36, 32, 60, 20, 57, 16, 50, 31, 19, 15, 30, 14, 13, 12
        };

        public static readonly Bitboard Zero = default;

        public static implicit operator ulong(Bitboard bb) => bb._value;
        public static implicit operator Bitboard(ulong value) => new Bitboard(value);

        public static bool operator ==(Bitboard left, Bitboard right) => left.Equals(right);
        public static bool operator !=(Bitboard left, Bitboard right) => !(left == right);

        private readonly ulong _value;

        public Bitboard(ulong value) => _value = value;

        public Bitboard ClearLsb()
        {
            unchecked
            {
                Debug.Assert(_value != 0);
                return new Bitboard(_value & (_value - 1));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfLsb()
        {
            unchecked
            {
                Debug.Assert(_value != 0);
                return MultiplyDeBruijnBitPosition[((_value & (ulong)(-(long)_value)) * 0x022FDD63CC95386DUL) >> 58];
            }
        }

        public int PopCount()
        {
            unchecked
            {
                ulong v = _value;
                v = v - ((v >> 1) & 0x5555555555555555UL);
                v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
                return (int)((((v + (v >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
            }
        }

        public override bool Equals(object obj) => obj is Bitboard other && Equals(other);

        public bool Equals(Bitboard other) => _value == other._value;

        public override int GetHashCode() => _value.GetHashCode();

        public override string ToString() => Convert.ToString(unchecked((long)_value), 2).PadLeft(64, '0');
    }
}