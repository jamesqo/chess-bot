using System;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    public struct Tile : IEquatable<Tile>
    {
        private readonly ushort _value;

        private const ushort LocationMask = 0b0000_0000_0011_1111;
        private const ushort HasPieceMask = 0b0000_0000_0100_0000;
        private const int HasPieceShift = 6;
        private const ushort PieceMask = 0b0000_0111_1000_0000;
        private const int PieceShift = HasPieceShift + 1;

        public Tile(Location location, Piece? piece = null)
        {
            bool hasPiece = (piece != null);
            var pieceOrDefault = piece ?? default;

            _value = (ushort)(location.Value | (Convert.ToInt32(hasPiece) << HasPieceShift) | (pieceOrDefault.Value << PieceShift));
        }

        public Location Location => new Location((byte)(_value & LocationMask));
        public bool HasPiece => Convert.ToBoolean((_value & HasPieceMask) >> HasPieceShift);
        public Piece Piece
        {
            get
            {
                if (!HasPiece) BadPieceCall();
                return new Piece((byte)((_value & PieceMask) >> PieceShift));
            }
        }

        // warning: this being true doesn't mean we can't be a valid Tile value!
        internal bool IsDefault => _value == 0;

        // We separate this out into a non-inlined method because we want to make it easy for the JIT to inline Piece
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Piece BadPieceCall() => throw new InvalidOperationException($".{nameof(Piece)} called on an empty tile");

        public override bool Equals(object obj) => obj is Tile other && Equals(other);

        public bool Equals(Tile other)
        {
            if (Location != other.Location) return false;
            return HasPiece
                ? other.HasPiece && Piece == other.Piece
                : !other.HasPiece;
        }

        public override int GetHashCode() => _value;

        public Tile SetPiece(Piece? piece) => new Tile(Location, piece);

        public override string ToString()
        {
            return HasPiece ? $"{Location} - {Piece}" : $"{Location} - empty";
        }
    }
}
