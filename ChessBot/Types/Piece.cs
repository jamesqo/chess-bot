using System;
using System.Diagnostics;
using static ChessBot.Types.PieceKind;
using static ChessBot.Types.Side;

namespace ChessBot.Types
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

        private readonly byte _value;

        private const byte SideMask = 0b0000_0001;
        private const byte KindMask = 0b0000_1110;
        private const int KindShift = 1;

        public Piece(Side side, PieceKind kind)
        {
            if (!side.IsValid()) throw new ArgumentOutOfRangeException(nameof(side));
            if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));

            _value = (byte)((int)side | ((int)kind << KindShift));
        }

        internal Piece(byte value)
        {
            _value = value;
            Debug.Assert(IsValid);
        }

        public Side Side => (Side)(_value & SideMask);
        public PieceKind Kind => (PieceKind)((_value & KindMask) >> KindShift);
        public bool IsWhite => Side.IsWhite();

        internal byte Value => _value;
        internal bool IsValid => Side.IsValid() && Kind.IsValid();

        public override bool Equals(object obj) => obj is Piece other && Equals(other);

        public bool Equals(Piece other) => _value == other._value;

        public override int GetHashCode() => _value;

        public char ToDisplayChar()
        {
            char result = Kind switch
            {
                Pawn => 'P',
                Knight => 'N',
                Bishop => 'B',
                Rook => 'R',
                Queen => 'Q',
                King => 'K',
            };
            if (!IsWhite) result = char.ToLowerInvariant(result);
            return result;
        }

        public override string ToString()
        {
            return $"{Side.ToString().ToLowerInvariant()} {Kind.ToString().ToLowerInvariant()}";
        }
    }
}
