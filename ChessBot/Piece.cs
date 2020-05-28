using System;
using static ChessBot.PieceKind;
using static ChessBot.PlayerColor;

namespace ChessBot
{
    public struct Piece : IEquatable<Piece>
    {
        public static Piece BlackPawn { get; } = new Piece(Black, Pawn);
        public static Piece BlackKnight { get; } = new Piece(Black, Knight);
        public static Piece BlackBishop { get; } = new Piece(Black, Bishop);
        public static Piece BlackRook { get; } = new Piece(Black, Rook);
        public static Piece BlackQueen { get; } = new Piece(Black, Queen);
        public static Piece BlackKing { get; } = new Piece(Black, King);
        public static Piece WhitePawn { get; } = new Piece(White, Pawn);
        public static Piece WhiteKnight { get; } = new Piece(White, Knight);
        public static Piece WhiteBishop { get; } = new Piece(White, Bishop);
        public static Piece WhiteRook { get; } = new Piece(White, Rook);
        public static Piece WhiteQueen { get; } = new Piece(White, Queen);
        public static Piece WhiteKing { get; } = new Piece(White, King);

        public static bool operator ==(Piece left, Piece right) => left.Equals(right);
        public static bool operator !=(Piece left, Piece right) => !(left == right);

        public Piece(PlayerColor color, PieceKind kind)
        {
            Color = Enum.IsDefined(typeof(PlayerColor), color) ? color : throw new ArgumentOutOfRangeException(nameof(color));
            Kind = Enum.IsDefined(typeof(PieceKind), kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        }

        public PlayerColor Color { get; }
        public PieceKind Kind { get; }

        public override bool Equals(object obj) => obj is Piece other && Equals(other);

        public bool Equals(Piece other)
            => Color == other.Color && Kind == other.Kind;

        public override int GetHashCode() => HashCode.Combine(Color, Kind);

        public override string ToString()
        {
            return $"{Color.ToString().ToLowerInvariant()} {Kind.ToString().ToLowerInvariant()}";
        }
    }
}
