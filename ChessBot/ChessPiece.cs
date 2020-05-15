using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static ChessBot.PieceKind;
using static ChessBot.PlayerColor;

namespace ChessBot
{
    public struct ChessPiece : IEquatable<ChessPiece>
    {
        public static ChessPiece BlackPawn { get; } = new ChessPiece(Black, Pawn);
        public static ChessPiece BlackKnight { get; } = new ChessPiece(Black, Knight);
        public static ChessPiece BlackBishop { get; } = new ChessPiece(Black, Bishop);
        public static ChessPiece BlackRook { get; } = new ChessPiece(Black, Rook);
        public static ChessPiece BlackQueen { get; } = new ChessPiece(Black, Queen);
        public static ChessPiece BlackKing { get; } = new ChessPiece(Black, King);
        public static ChessPiece WhitePawn { get; } = new ChessPiece(White, Pawn);
        public static ChessPiece WhiteKnight { get; } = new ChessPiece(White, Knight);
        public static ChessPiece WhiteBishop { get; } = new ChessPiece(White, Bishop);
        public static ChessPiece WhiteRook { get; } = new ChessPiece(White, Rook);
        public static ChessPiece WhiteQueen { get; } = new ChessPiece(White, Queen);
        public static ChessPiece WhiteKing { get; } = new ChessPiece(White, King);

        public static bool operator ==(ChessPiece left, ChessPiece right) => left.Equals(right);
        public static bool operator !=(ChessPiece left, ChessPiece right) => !(left == right);

        public ChessPiece(PlayerColor color, PieceKind kind)
        {
            Color = Enum.IsDefined(typeof(PlayerColor), color) ? color : throw new ArgumentOutOfRangeException(nameof(color));
            Kind = Enum.IsDefined(typeof(PieceKind), kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        }

        public PlayerColor Color { get; }
        public PieceKind Kind { get; }

        public override bool Equals(object obj) => obj is ChessPiece other && Equals(other);

        public bool Equals(ChessPiece other)
            => Color == other.Color && Kind == other.Kind;

        public override int GetHashCode() => HashCode.Combine(Color, Kind);

        public override string ToString()
        {
            return $"{Color.ToString().ToLowerInvariant()} {Kind.ToString().ToLowerInvariant()}";
        }
    }
}
