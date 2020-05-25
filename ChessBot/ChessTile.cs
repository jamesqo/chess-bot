using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChessBot
{
    public class ChessTile : IEquatable<ChessTile>
    {
        private readonly ChessPiece _piece;

        public ChessTile(BoardLocation location, ChessPiece? piece = null)
        {
            Location = location;
            HasPiece = (piece != null);
            _piece = piece ?? default;
        }

        public BoardLocation Location { get; }

        public bool HasPiece { get; }
        public ChessPiece Piece
        {
            get
            {
                if (!HasPiece) BadPieceCall();
                return _piece;
            }
        }

        // We separate this out into another, non-inlined method because we want to make it easy for the JIT to inline get_Piece()
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ChessPiece BadPieceCall() => throw new InvalidOperationException($".{nameof(Piece)} called on an empty tile");

        public override bool Equals(object obj) => Equals(obj as ChessTile);

        public bool Equals([AllowNull] ChessTile other)
        {
            if (other == null || Location != other.Location) return false;
            return HasPiece
                ? other.HasPiece && Piece == other.Piece
                : !other.HasPiece;
        }

        public override int GetHashCode() => throw new NotImplementedException();

        public ChessTile SetPiece(ChessPiece? piece) => new ChessTile(Location, piece);

        public override string ToString()
        {
            return HasPiece ? $"{Location} - {_piece}" : $"{Location} - empty";
        }
    }
}
