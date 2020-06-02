using System;
using System.Diagnostics;
using static ChessBot.Types.PieceKind;
using static ChessBot.Types.Side;

namespace ChessBot.Types
{
    public struct Piece : IEquatable<Piece>
    {
        public const int NumberOfValues = 12;

        public static readonly Piece BlackPawn = new Piece(Black, Pawn);
        public static readonly Piece BlackKnight = new Piece(Black, Knight);
        public static readonly Piece BlackBishop = new Piece(Black, Bishop);
        public static readonly Piece BlackRook = new Piece(Black, Rook);
        public static readonly Piece BlackQueen = new Piece(Black, Queen);
        public static readonly Piece BlackKing = new Piece(Black, King);
        public static readonly Piece WhitePawn = new Piece(White, Pawn);
        public static readonly Piece WhiteKnight = new Piece(White, Knight);
        public static readonly Piece WhiteBishop = new Piece(White, Bishop);
        public static readonly Piece WhiteRook = new Piece(White, Rook);
        public static readonly Piece WhiteQueen = new Piece(White, Queen);
        public static readonly Piece WhiteKing = new Piece(White, King);

        public static bool operator ==(Piece left, Piece right) => left.Equals(right);
        public static bool operator !=(Piece left, Piece right) => !(left == right);

        internal static Piece FromIndex(int index)
        {
            Debug.Assert(index >= 0 && index < NumberOfValues);
            var side = (Side)(index / 6);
            var kind = (PieceKind)(index % 6);
            return new Piece(side, kind);
        }

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

        // We can't just return _value since it needs to be contiguous
        internal int ToIndex() => (int)Side * 6 + (int)Kind;
    }
}
