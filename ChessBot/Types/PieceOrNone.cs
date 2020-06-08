using System.Diagnostics;

namespace ChessBot.Types
{
    // todo: decide if this is really worth it
    public readonly struct PieceOrNone
    {
        public static readonly PieceOrNone None = default;

        public static implicit operator PieceOrNone(Piece piece) => new PieceOrNone((byte)(piece.Value + 1));

        private readonly byte _value;

        internal PieceOrNone(byte value)
        {
            _value = value;
            Debug.Assert(IsValid);
        }

        public bool HasPiece => _value != 0;
        public Piece Piece
        {
            get
            {
                Debug.Assert(HasPiece);
                return new Piece((byte)(_value - 1));
            }
        }

        internal byte Value => _value;
        internal bool IsValid => (!HasPiece || Piece.IsValid);
    }
}
