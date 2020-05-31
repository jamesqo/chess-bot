using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    public class Tile : IEquatable<Tile>
    {
        private readonly Piece _piece;

        public Tile(Location location, Piece? piece = null)
        {
            Location = location;
            HasPiece = (piece != null);
            _piece = piece ?? default;
        }

        public Location Location { get; }

        public bool HasPiece { get; }
        public Piece Piece
        {
            get
            {
                if (!HasPiece) BadPieceCall();
                return _piece;
            }
        }

        // We separate this out into a non-inlined method because we want to make it easy for the JIT to inline get_Piece()
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Piece BadPieceCall() => throw new InvalidOperationException($".{nameof(Piece)} called on an empty tile");

        public override bool Equals(object obj) => Equals(obj as Tile);

        public bool Equals([AllowNull] Tile other)
        {
            if (other == null || Location != other.Location) return false;
            return HasPiece
                ? other.HasPiece && Piece == other.Piece
                : !other.HasPiece;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.Add(Location);
            if (HasPiece)
            {
                hc.Add(Piece);
            }
            return hc.ToHashCode();
        }

        public Tile SetPiece(Piece? piece) => new Tile(Location, piece);

        public override string ToString()
        {
            return HasPiece ? $"{Location} - {_piece}" : $"{Location} - empty";
        }
    }
}
