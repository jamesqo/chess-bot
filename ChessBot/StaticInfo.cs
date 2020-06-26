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
        #region Initialization logic

        private struct MagicInfo
        {
            public Bitboard Mask;
            public Bitboard Magic;
            public int Shift;
            public Bitboard[] AttackBitboards; 
        }

        private static readonly Bitboard[] BishopMagics = new Bitboard[64]
        {
            0x89a1121896040240UL, 0x2004844802002010UL, 0x2068080051921000UL, 0x62880a0220200808UL, 0x4042004000000UL,
            0x100822020200011UL, 0xc00444222012000aUL, 0x28808801216001UL, 0x400492088408100UL, 0x201c401040c0084UL,
            0x840800910a0010UL, 0x82080240060UL, 0x2000840504006000UL, 0x30010c4108405004UL, 0x1008005410080802UL,
            0x8144042209100900UL, 0x208081020014400UL, 0x4800201208ca00UL, 0xf18140408012008UL, 0x1004002802102001UL,
            0x841000820080811UL, 0x40200200a42008UL, 0x800054042000UL, 0x88010400410c9000UL, 0x520040470104290UL,
            0x1004040051500081UL, 0x2002081833080021UL, 0x400c00c010142UL, 0x941408200c002000UL, 0x658810000806011UL,
            0x188071040440a00UL, 0x4800404002011c00UL, 0x104442040404200UL, 0x511080202091021UL, 0x4022401120400UL,
            0x80c0040400080120UL, 0x8040010040820802UL, 0x480810700020090UL, 0x102008e00040242UL, 0x809005202050100UL,
            0x8002024220104080UL, 0x431008804142000UL, 0x19001802081400UL, 0x200014208040080UL, 0x3308082008200100UL,
            0x41010500040c020UL, 0x4012020c04210308UL, 0x208220a202004080UL, 0x111040120082000UL, 0x6803040141280a00UL,
            0x2101004202410000UL, 0x8200000041108022UL, 0x21082088000UL, 0x2410204010040UL, 0x40100400809000UL,
            0x822088220820214UL, 0x40808090012004UL, 0x910224040218c9UL, 0x402814422015008UL, 0x90014004842410UL,
            0x1000042304105UL, 0x10008830412a00UL, 0x2520081090008908UL, 0x40102000a0a60140UL,
        };

        private static readonly Bitboard[] RookMagics = new Bitboard[64]
        {
            0xa8002c000108020UL, 0x6c00049b0002001UL, 0x100200010090040UL, 0x2480041000800801UL, 0x280028004000800UL,
            0x900410008040022UL, 0x280020001001080UL, 0x2880002041000080UL, 0xa000800080400034UL, 0x4808020004000UL,
            0x2290802004801000UL, 0x411000d00100020UL, 0x402800800040080UL, 0xb000401004208UL, 0x2409000100040200UL,
            0x1002100004082UL, 0x22878001e24000UL, 0x1090810021004010UL, 0x801030040200012UL, 0x500808008001000UL,
            0xa08018014000880UL, 0x8000808004000200UL, 0x201008080010200UL, 0x801020000441091UL, 0x800080204005UL,
            0x1040200040100048UL, 0x120200402082UL, 0xd14880480100080UL, 0x12040280080080UL, 0x100040080020080UL,
            0x9020010080800200UL, 0x813241200148449UL, 0x491604001800080UL, 0x100401000402001UL, 0x4820010021001040UL,
            0x400402202000812UL, 0x209009005000802UL, 0x810800601800400UL, 0x4301083214000150UL, 0x204026458e001401UL,
            0x40204000808000UL, 0x8001008040010020UL, 0x8410820820420010UL, 0x1003001000090020UL, 0x804040008008080UL,
            0x12000810020004UL, 0x1000100200040208UL, 0x430000a044020001UL, 0x280009023410300UL, 0xe0100040002240UL,
            0x200100401700UL, 0x2244100408008080UL, 0x8000400801980UL, 0x2000810040200UL, 0x8010100228810400UL,
            0x2000009044210200UL, 0x4080008040102101UL, 0x40002080411d01UL, 0x2005524060000901UL, 0x502001008400422UL,
            0x489a000810200402UL, 0x1004400080a13UL, 0x4000011008020084UL, 0x26002114058042UL
        };

        private static readonly MagicInfo[] BishopInfos = new MagicInfo[64];
        private static readonly MagicInfo[] RookInfos = new MagicInfo[64];

        private static readonly Bitboard[][] PawnAttackBitboards = new Bitboard[2][];
        private static readonly Bitboard[][] NoContextAttackBitboards = new Bitboard[6][]; // knight and king

        static StaticInfo()
        {
            for (int i = 0; i < Location.NumberOfValues; i++)
            {
                var source = Location.FromIndex(i);
                ref MagicInfo info = ref BishopInfos[i];
                var occupancy = ComputeBishopOccupancy(source);
                info.Mask = occupancy;

                var magic = BishopMagics[i];
                info.Magic = magic;

                int numBits = occupancy.CountSetBits();
                Debug.Assert(numBits <= 11);
                int shift = 64 - numBits;
                int numValues = 1 << numBits;
                info.Shift = shift;
                info.AttackBitboards = new Bitboard[numValues];

                foreach (var blockers in occupancy.PowerSet())
                {
                    ushort key = (ushort)((blockers * magic) >> shift);
                    info.AttackBitboards[key] = ComputeBishopAttacks(source, blockers);
                }
            }

            for (int i = 0; i < Location.NumberOfValues; i++)
            {
                var source = Location.FromIndex(i);
                ref MagicInfo info = ref RookInfos[i];
                var occupancy = ComputeRookOccupancy(source);
                info.Mask = occupancy;

                var magic = RookMagics[i];
                info.Magic = magic;

                int numBits = occupancy.CountSetBits();
                Debug.Assert(numBits <= 13);
                int shift = 64 - numBits;
                int numValues = 1 << numBits;
                info.Shift = shift;
                info.AttackBitboards = new Bitboard[numValues];

                foreach (var blockers in occupancy.PowerSet())
                {
                    ushort key = (ushort)((blockers * magic) >> shift);
                    info.AttackBitboards[key] = ComputeRookAttacks(source, blockers);
                }
            }

            foreach (var side in new[] { Side.White, Side.Black })
            {
                PawnAttackBitboards[(int)side] = new Bitboard[64];
                for (int i = 0; i < Location.NumberOfValues; i++)
                {
                    PawnAttackBitboards[(int)side][i] = ComputePawnAttacks(Location.FromIndex(i), side);
                }
            }

            foreach (var kind in new[] { PieceKind.Knight, PieceKind.King })
            {
                NoContextAttackBitboards[(int)kind] = new Bitboard[64];
                for (int i = 0; i < Location.NumberOfValues; i++)
                {
                    var source = Location.FromIndex(i);
                    NoContextAttackBitboards[(int)kind][i] = ComputeNoContextAttacks(source, kind);
                }
            }
        }

        private static Bitboard ComputeBishopAttacks(Location source, Bitboard blockers)
        {
            Debug.Assert(source.IsValid);
            Debug.Assert((blockers & source.GetMask()) == 0);

            var attacks = Bitboard.Zero;
            Location next;

            // Northeast
            for (var prev = source; prev.Rank < Rank8 && prev.File < FileH; prev = next)
            {
                next = prev.Add(1, 1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            // Southeast
            for (var prev = source; prev.Rank > Rank1 && prev.File < FileH; prev = next)
            {
                next = prev.Add(1, -1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            // Southwest
            for (var prev = source; prev.Rank > Rank1 && prev.File > FileA; prev = next)
            {
                next = prev.Add(-1, -1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            // Northwest
            for (var prev = source; prev.Rank < Rank8 && prev.File > FileA; prev = next)
            {
                next = prev.Add(-1, 1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            return attacks;
        }

        private static Bitboard ComputeRookAttacks(Location source, Bitboard blockers)
        {
            Debug.Assert(source.IsValid);
            Debug.Assert((blockers & source.GetMask()) == 0);

            var attacks = Bitboard.Zero;
            Location next;

            // North
            for (var prev = source; prev.Rank < Rank8; prev = next)
            {
                next = prev.Up(1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            // East
            for (var prev = source; prev.File < FileH; prev = next)
            {
                next = prev.Right(1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            // South
            for (var prev = source; prev.Rank > Rank1; prev = next)
            {
                next = prev.Down(1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            // West
            for (var prev = source; prev.File > FileA; prev = next)
            {
                next = prev.Left(1);
                attacks |= next.GetMask();
                if (blockers[next]) break;
            }

            return attacks;
        }

        private static Bitboard ComputePawnAttacks(Location source, Side side)
        {
            Debug.Assert(source.IsValid);
            Debug.Assert(side.IsValid());

            var result = Bitboard.CreateBuilder();

            if (source.Rank != EighthRank(side))
            {
                if (source.File > FileA) result.Set(source.Up(ForwardStep(side)).Left(1));
                if (source.File < FileH) result.Set(source.Up(ForwardStep(side)).Right(1));
            }

            return result.Value;
        }

        private static Bitboard ComputeNoContextAttacks(Location source, PieceKind kind)
        {
            Debug.Assert(source.IsValid);
            Debug.Assert(kind == PieceKind.Knight || kind == PieceKind.King);

            var result = Bitboard.CreateBuilder();

            switch (kind)
            {
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
            }

            return result.Value;
        }

        private static Bitboard ComputeBishopOccupancy(Location source)
        {
            Debug.Assert(source.IsValid);

            // We only care about inner squares
            var result = Bitboard.Zero;

            // Northeast
            var prev = source;
            while (prev.Rank < Rank7 && prev.File < FileG)
            {
                var next = prev.Up(1).Right(1);
                result |= next.GetMask();
                prev = next;
            }

            // Southeast
            prev = source;
            while (prev.Rank > Rank2 && prev.File < FileG)
            {
                var next = prev.Down(1).Right(1);
                result |= next.GetMask();
                prev = next;
            }

            // Southwest
            prev = source;
            while (prev.Rank > Rank2 && prev.File > FileB)
            {
                var next = prev.Down(1).Left(1);
                result |= next.GetMask();
                prev = next;
            }

            // Northwest
            prev = source;
            while (prev.Rank < Rank7 && prev.File > FileB)
            {
                var next = prev.Up(1).Left(1);
                result |= next.GetMask();
                prev = next;
            }

            return result;
        }

        private static Bitboard ComputeRookOccupancy(Location source)
        {
            Debug.Assert(source.IsValid);

            // We only care about inner squares
            var result = Bitboard.Zero;

            // East
            var prev = source;
            while (prev.File < FileG)
            {
                var next = prev.Right(1);
                result |= next.GetMask();
                prev = next;
            }

            // West
            prev = source;
            while (prev.File > FileB)
            {
                var next = prev.Left(1);
                result |= next.GetMask();
                prev = next;
            }

            // North
            prev = source;
            while (prev.Rank < Rank7)
            {
                var next = prev.Up(1);
                result |= next.GetMask();
                prev = next;
            }

            // South
            prev = source;
            while (prev.Rank > Rank2)
            {
                var next = prev.Down(1);
                result |= next.GetMask();
                prev = next;
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Gets the attack bitboard for <paramref name="piece"/> at <paramref name="source"/>.
        /// </summary>
        public static Bitboard GetAttackBitboard(Piece piece, Location source, Bitboard occupied)
        {
            if (!piece.IsValid) throw new ArgumentException("", nameof(piece));
            if (!source.IsValid) throw new ArgumentException("", nameof(source));

            var kind = piece.Kind;
            switch (kind)
            {
                case PieceKind.Pawn:
                    return PawnAttackBitboards[(int)piece.Side][source.ToIndex()];
                case PieceKind.Bishop:
                case PieceKind.Rook:
                    var info = (kind == PieceKind.Bishop ? BishopInfos : RookInfos)[source.ToIndex()];
                    var blockers = occupied & info.Mask;
                    ushort key = (ushort)((blockers * info.Magic) >> info.Shift);
                    return info.AttackBitboards[key];
                case PieceKind.Queen:
                    // todo: optimize
                    return GetAttackBitboard(Piece.WhiteBishop, source, occupied) | GetAttackBitboard(Piece.WhiteRook, source, occupied);
                default:
                    return NoContextAttackBitboards[(int)kind][source.ToIndex()];
            }
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
    }
}
