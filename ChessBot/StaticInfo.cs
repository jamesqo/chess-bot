using ChessBot.Types;
using System;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot
{
    internal static class StaticInfo
    {
        public static Location GetStartLocation(Side side, PieceKind kind, bool? kingside = null)
        {
            if (!side.IsValid()) throw new ArgumentOutOfRangeException(nameof(side));
            bool isKindValid = kingside.HasValue
                ? (kind >= PieceKind.Knight || kind <= PieceKind.Rook)
                : (kind == PieceKind.Queen || kind == PieceKind.King);
            if (!isKindValid) throw new ArgumentOutOfRangeException(nameof(kind));

            var rank = BackRank(side);
            return kind switch
            {
                PieceKind.Knight => (kingside.Value ? FileG : FileB, rank),
                PieceKind.Bishop => (kingside.Value ? FileF : FileC, rank),
                PieceKind.Rook => (kingside.Value ? FileH : FileA, rank),
                PieceKind.Queen => (FileD, rank),
                PieceKind.King => (FileE, rank),
            };
        }

        public static Rank BackRank(Side side) => side.IsWhite() ? Rank1 : Rank8;
        public static Rank SecondRank(Side side) => side.IsWhite() ? Rank2 : Rank7;
        public static Rank SeventhRank(Side side) => side.IsWhite() ? Rank7 : Rank2;
        public static Rank EighthRank(Side side) => side.IsWhite() ? Rank8 : Rank1;

        public static int ForwardStep(Side side) => side.IsWhite() ? 1 : -1;
    }
}
