using System.Diagnostics;

namespace ChessBot.Types
{
    public enum PieceKind
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    public static class PieceKindHelpers
    {
        public static bool IsValid(this PieceKind kind)
            => kind >= PieceKind.Pawn && kind <= PieceKind.King;
    }
}
