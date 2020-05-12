using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    public class ChessTile
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
        public ChessPiece Piece =>
            HasPiece ? _piece : throw new InvalidOperationException();

        public ChessTile WithPiece(ChessPiece? piece) => new ChessTile(Location, piece);

        public override string ToString()
        {
            return HasPiece ? $"{Location} - {_piece}" : $"{Location} - empty";
        }
    }
}
