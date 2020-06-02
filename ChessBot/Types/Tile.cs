using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    public struct Tile : IEquatable<Tile>
    {
        private readonly ushort _value;

        private const ushort LocationMask = 0b0000_0000_0011_1111;
        private const ushort PieceMask = 0b0000_0011_1100_0000;
        private const int PieceShift = Location.NumberOfBits;

        public Tile(Location location, Piece? piece = null)
        {
            int pieceValue = (piece?.Value + 1) ?? 0;
            _value = (ushort)(location.Value | (pieceValue << PieceShift));
        }

        internal Tile(ushort value)
        {
            _value = value;
            Debug.Assert(IsValid);
        }

        public Location Location => new Location((byte)(_value & LocationMask));
        public bool HasPiece => (_value & PieceMask) != 0;
        public Piece Piece
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int pieceValue = (_value & PieceMask) >> PieceShift;
                if (pieceValue == 0) BadPieceCall();
                return new Piece((byte)(pieceValue - 1));
            }
        }

        // warning: this being true doesn't mean we can't be a valid Tile value!
        internal bool IsDefault => _value == 0;
        internal bool IsValid => Location.IsValid && (!HasPiece || Piece.IsValid);

        // We separate this out into a non-inlined method because we want to make it easy for the JIT to inline Piece
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Piece BadPieceCall() => throw new InvalidOperationException($".{nameof(Piece)} called on an empty tile");

        public override bool Equals(object obj) => obj is Tile other && Equals(other);

        public bool Equals(Tile other) => _value == other._value;

        public override int GetHashCode() => _value;

        public Tile SetPiece(Piece? piece) => new Tile(Location, piece);

        public override string ToString()
        {
            return HasPiece ? $"{Location} - {Piece}" : $"{Location} - empty";
        }
    }
}
