using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Diagnostics;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot
{
    /// <summary>
    /// Helper methods to deal with fixed information about chess games.
    /// </summary>
    internal static class StaticInfo
    {
        private static readonly Bitboard[,] AttackBitboards;
        private static readonly Bitboard[,] StopMasks;

        static StaticInfo()
        {
            AttackBitboards = new Bitboard[6, 64];
            for (var kind = PieceKind.Pawn; kind <= PieceKind.King; kind++)
            {
                for (int i = 0; i < Location.NumberOfValues; i++)
                {
                    var source = Location.FromIndex(i);
                    AttackBitboards[(int)kind, i] = ComputeAttackBitboard(kind, source);
                }
            }

            StopMasks = new Bitboard[8, 64];
            for (var d = Direction.Start; d <= Direction.End; d++)
            {
                for (int i = 0; i < Location.NumberOfValues; i++)
                {
                    var location = Location.FromIndex(i);
                    StopMasks[(int)d, i] = ComputeStopMask(location, d);
                }
            }
        }

        /// <summary>
        /// Gets the attack bitboard for <paramref name="piece"/> at <paramref name="source"/> if it were unrestricted by other pieces on the board.
        /// </summary>
        public static Bitboard GetAttackBitboard(Piece piece, Location source)
        {
            if (!piece.IsValid) throw new ArgumentException("", nameof(piece));
            if (!source.IsValid) throw new ArgumentException("", nameof(source));

            int sourceIndex = source.ToIndex();
            if (piece == Piece.BlackPawn)
            {
                // The pawn attack bitboards are stored from the perspective of white.
                // For black pawns, we take the pawn attack bitboard at the mirror location (eg. b2 -> g7) and flip it.
                sourceIndex = (Location.NumberOfValues - 1) - sourceIndex;
            }
            var result = AttackBitboards[(int)piece.Kind, sourceIndex];
            if (piece == Piece.BlackPawn)
            {
                result = result.Reverse();
            }
            return result;
        }

        public static Bitboard GetStopMask(Location location, Direction direction)
        {
            // todo: arg validation
            return StopMasks[(int)direction, location.ToIndex()];
        }

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

        private static Bitboard ComputeAttackBitboard(PieceKind kind, Location source)
        {
            var result = new BitboardBuilder();

            switch (kind)
            {
                case PieceKind.Bishop:
                    result.SetRange(GetDiagonalExtension(source));
                    break;
                case PieceKind.King:
                    if (source.Rank > Rank1)
                    {
                        if (source.File > FileA) result.Set(source.Down(1).Left(1));
                        result.Set(source.Down(1));
                        if (source.File < FileH) result.Set(source.Down(1).Right(1));
                    }
                    if (source.File > FileA) result.Set(source.Left(1));
                    if (source.File < FileH) result.Set(source.Right(1));
                    if (source.Rank < Rank8)
                    {
                        if (source.File > FileA) result.Set(source.Up(1).Left(1));
                        result.Set(source.Up(1));
                        if (source.File < FileH) result.Set(source.Up(1).Right(1));
                    }
                    // It isn't possible to capture by castling, so we can safely ignore that scenario.
                    break;
                case PieceKind.Knight:
                    if (source.Rank > Rank1 && source.File > FileB) result.Set(source.Down(1).Left(2));
                    if (source.Rank < Rank8 && source.File > FileB) result.Set(source.Up(1).Left(2));
                    if (source.Rank > Rank1 && source.File < FileG) result.Set(source.Down(1).Right(2));
                    if (source.Rank < Rank8 && source.File < FileG) result.Set(source.Up(1).Right(2));
                    if (source.Rank > Rank2 && source.File > FileA) result.Set(source.Down(2).Left(1));
                    if (source.Rank < Rank7 && source.File > FileA) result.Set(source.Up(2).Left(1));
                    if (source.Rank > Rank2 && source.File < FileH) result.Set(source.Down(2).Right(1));
                    if (source.Rank < Rank7 && source.File < FileH) result.Set(source.Up(2).Right(1));
                    break;
                case PieceKind.Pawn:
                    // we just return it from white's perspective. the caller flips it for black
                    if (source.Rank < Rank8)
                    {
                        if (source.File > FileA) result.Set(source.Up(1).Left(1));
                        if (source.File < FileH) result.Set(source.Up(1).Right(1));
                    }
                    break;
                case PieceKind.Queen:
                    result.SetRange(GetDiagonalExtension(source));
                    result.SetRange(GetOrthogonalExtension(source));
                    break;
                case PieceKind.Rook:
                    result.SetRange(GetOrthogonalExtension(source));
                    break;
            }

            return result.Bitboard;
        }

        private static Bitboard GetDiagonalExtension(Location source)
        {
            var result = Bitboard.Zero;

            // Northeast
            var prev = source;
            while (prev.Rank < Rank8 && prev.File < FileH)
            {
                var next = prev.Up(1).Right(1);
                result |= next.GetMask();
                prev = next;
            }

            // Southeast
            prev = source;
            while (prev.Rank > Rank1 && prev.File < FileH)
            {
                var next = prev.Down(1).Right(1);
                result |= next.GetMask();
                prev = next;
            }

            // Southwest
            prev = source;
            while (prev.Rank > Rank1 && prev.File > FileA)
            {
                var next = prev.Down(1).Left(1);
                result |= next.GetMask();
                prev = next;
            }

            // Northwest
            prev = source;
            while (prev.Rank < Rank8 && prev.File > FileA)
            {
                var next = prev.Up(1).Left(1);
                result |= next.GetMask();
                prev = next;
            }

            return result;
        }

        private static Bitboard GetOrthogonalExtension(Location source)
        {
            var result = Bitboard.Zero;

            // East
            var prev = source;
            while (prev.File < FileH)
            {
                var next = prev.Right(1);
                result |= next.GetMask();
                prev = next;
            }

            // West
            prev = source;
            while (prev.File > FileA)
            {
                var next = prev.Left(1);
                result |= next.GetMask();
                prev = next;
            }

            // North
            prev = source;
            while (prev.Rank < Rank8)
            {
                var next = prev.Up(1);
                result |= next.GetMask();
                prev = next;
            }

            // South
            prev = source;
            while (prev.Rank > Rank1)
            {
                var next = prev.Down(1);
                result |= next.GetMask();
                prev = next;
            }

            return result;
        }

        private static Bitboard ComputeStopMask(Location location, Direction direction)
        {
            var result = new BitboardBuilder(Bitboard.AllOnes);
            Location next;
            switch (direction)
            {
                case Direction.North:
                    for (var prev = location; prev.Rank < Rank8; prev = next)
                    {
                        next = prev.Up(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.East:
                    for (var prev = location; prev.File < FileH; prev = next)
                    {
                        next = prev.Right(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.South:
                    for (var prev = location; prev.Rank > Rank1; prev = next)
                    {
                        next = prev.Down(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.West:
                    for (var prev = location; prev.File > FileA; prev = next)
                    {
                        next = prev.Left(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.Northeast:
                    for (var prev = location; prev.Rank < Rank8 && prev.File < FileH; prev = next)
                    {
                        next = prev.Up(1).Right(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.Southeast:
                    for (var prev = location; prev.Rank > Rank1 && prev.File < FileH; prev = next)
                    {
                        next = prev.Down(1).Right(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.Southwest:
                    for (var prev = location; prev.Rank > Rank1 && prev.File > FileA; prev = next)
                    {
                        next = prev.Down(1).Left(1);
                        result.Clear(next);
                    }
                    break;
                case Direction.Northwest:
                    for (var prev = location; prev.Rank < Rank8 && prev.File > FileA; prev = next)
                    {
                        next = prev.Up(1).Left(1);
                        result.Clear(next);
                    }
                    break;
            }
            return result.Bitboard;
        }
    }
}
