﻿using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        }

        /// <summary>
        /// Gets the attack bitboard for <paramref name="piece"/> at <paramref name="source"/> if it were unrestricted by other pieces on the board.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard GetAttackBitboard(Piece piece, Location source)
        {
            //if (!piece.IsValid) throw new ArgumentException("", nameof(piece));
            //if (!source.IsValid) throw new ArgumentException("", nameof(source));
            Debug.Assert(piece.IsValid);
            Debug.Assert(source.IsValid);

            return piece != Piece.BlackPawn
                ? AttackBitboards[(int)piece.Kind, source.ToIndex()]
                : GetAttackBitboardForBlackPawn(source);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Bitboard GetAttackBitboardForBlackPawn(Location source)
        {
            // The pawn attack bitboards are stored from the perspective of white.
            // For black pawns, we take the pawn attack bitboard at the mirror location (eg. b2 -> g7) and flip it.
            int sourceIndex = (Location.NumberOfValues - 1) - source.ToIndex();
            return AttackBitboards[(int)PieceKind.Pawn, sourceIndex].Reverse();
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

        public static CastlingRights GetCastleFlags(Side side) => GetKingsideCastleFlag(side) | GetQueensideCastleFlag(side);
        public static CastlingRights GetKingsideCastleFlag(Side side) => side.IsWhite() ? CastlingRights.K : CastlingRights.k;
        public static CastlingRights GetQueensideCastleFlag(Side side) => side.IsWhite() ? CastlingRights.Q : CastlingRights.q;

        public static Rank BackRank(Side side) => side.IsWhite() ? Rank1 : Rank8;
        public static Rank SecondRank(Side side) => side.IsWhite() ? Rank2 : Rank7;
        public static Rank SeventhRank(Side side) => side.IsWhite() ? Rank7 : Rank2;
        public static Rank EighthRank(Side side) => side.IsWhite() ? Rank8 : Rank1;

        public static int ForwardStep(Side side) => side.IsWhite() ? 1 : -1;

        private static Bitboard ComputeAttackBitboard(PieceKind kind, Location source)
        {
            var result = Bitboard.CreateBuilder();

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

            return result.Value;
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
    }
}
