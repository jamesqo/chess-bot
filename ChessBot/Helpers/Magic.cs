using ChessBot.Types;
using System.Diagnostics;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot.Helpers
{
    // todo: the code here could be consolidated with StaticInfo
    /// <summary>
    /// Uses magic bitboards to quickly compute sliding piece (bishop, rook, queen) attack vectors.
    /// </summary>
    internal static class Magic
    {
        private static readonly Bitboard InnerSquares = ComputeInnerSquares();

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

        private static readonly int[] BishopShifts = new int[64];
        private static readonly int[] RookShifts = new int[64];

        private static readonly Bitboard[][] BishopTables = new Bitboard[64][];
        private static readonly Bitboard[][] RookTables = new Bitboard[64][];

        static Magic()
        {
            for (int i = 0; i < Location.NumberOfValues; i++)
            {
                var source = new Location((byte)i);
                var occupancy = BishopOccupancy(StaticInfo.GetAttackBitboard(Piece.WhiteBishop, source));

                int numBits = occupancy.CountSetBits();
                Debug.Assert(numBits <= 11);
                int shift = 64 - numBits;
                int numValues = 1 << numBits;
                BishopShifts[i] = shift;
                BishopTables[i] = new Bitboard[numValues];

                foreach (var blockers in occupancy.PowerSet())
                {
                    ushort key = (ushort)((blockers * BishopMagics[i]) >> shift);
                    BishopTables[i][key] = ComputeBishopAttacks(source, blockers);
                }
            }

            for (int i = 0; i < Location.NumberOfValues; i++)
            {
                var source = new Location((byte)i);
                var occupancy = RookOccupancy(StaticInfo.GetAttackBitboard(Piece.WhiteRook, source), source);

                int numBits = occupancy.CountSetBits();
                Debug.Assert(numBits <= 13);
                int shift = 64 - numBits;
                int numValues = 1 << numBits;
                RookShifts[i] = shift;
                RookTables[i] = new Bitboard[numValues];

                foreach (var blockers in occupancy.PowerSet())
                {
                    ushort key = (ushort)((blockers * RookMagics[i]) >> shift);
                    RookTables[i][key] = ComputeRookAttacks(source, blockers);
                }
            }
        }

        public static Bitboard BishopAttacks(Bitboard attacks, Bitboard occupied, Location source)
        {
            var occupancy = BishopOccupancy(attacks);
            var blockers = occupancy & occupied;
            int index = source.Value;

            ushort key = (ushort)((blockers * BishopMagics[index]) >> BishopShifts[index]);
            return BishopTables[index][key];
        }

        public static Bitboard RookAttacks(Bitboard attacks, Bitboard occupied, Location source)
        {
            var occupancy = RookOccupancy(attacks, source);
            var blockers = occupancy & occupied;
            int index = source.Value;

            ushort key = (ushort)((blockers * RookMagics[index]) >> RookShifts[index]);
            return RookTables[index][key];
        }

        private static Bitboard BishopOccupancy(Bitboard attacks) => attacks & InnerSquares;

        private static Bitboard RookOccupancy(Bitboard attacks, Location source)
        {
            var occupancy = attacks;
            occupancy &= ~new Location(FileA, source.Rank).GetMask();
            occupancy &= ~new Location(FileH, source.Rank).GetMask();
            occupancy &= ~new Location(source.File, Rank1).GetMask();
            occupancy &= ~new Location(source.File, Rank8).GetMask();
            return occupancy;
        }

        private static Bitboard ComputeInnerSquares()
        {
            var result = Bitboard.Zero;
            for (var file = FileB; file <= FileG; file++)
            {
                for (var rank = Rank2; rank <= Rank7; rank++)
                {
                    result |= new Location(file, rank).GetMask();
                }
            }
            return result;
        }

        private static Bitboard ComputeBishopAttacks(Location source, Bitboard blockers)
        {
            Debug.Assert(source.IsValid);
            Debug.Assert(!blockers.OverlapsWith(source.GetMask()));

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
            Debug.Assert(!blockers.OverlapsWith(source.GetMask()));

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
    }
}
