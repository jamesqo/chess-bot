using ChessBot.Types;
using System.Diagnostics;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot.Helpers
{
    /// <summary>
    /// Uses magic bitboards to quickly compute sliding piece (bishop, rook, queen) attack vectors.
    /// </summary>
    internal static class Magic
    {
        private static readonly Bitboard InnerSquares = ComputeInnerSquares();
        private static readonly Bitboard OuterSquares = ~InnerSquares;

        // Generated via the program at https://www.chessprogramming.org/Looking_for_Magics
        private static readonly Bitboard[] BishopMagics = {
            0x100420000431024UL,
            0x280800101073404UL,
            0x42000a00840802UL,
            0xca800c0410c2UL,
            0x81004290941c20UL,
            0x400200450020250UL,
            0x444a019204022084UL,
            0x88610802202109aUL,
            0x11210a0800086008UL,
            0x400a08c08802801UL,
            0x1301a0500111c808UL,
            0x1280100480180404UL,
            0x720009020028445UL,
            0x91880a9000010a01UL,
            0x31200940150802b2UL,
            0x5119080c20000602UL,
            0x242400a002448023UL,
            0x4819006001200008UL,
            0x222c10400020090UL,
            0x302008420409004UL,
            0x504200070009045UL,
            0x210071240c02046UL,
            0x1182219000022611UL,
            0x400c50000005801UL,
            0x4004010000113100UL,
            0x2008121604819400UL,
            0xc4a4010000290101UL,
            0x404a000888004802UL,
            0x8820c004105010UL,
            0x28280100908300UL,
            0x4c013189c0320a80UL,
            0x42008080042080UL,
            0x90803000c080840UL,
            0x2180001028220UL,
            0x1084002a040036UL,
            0x212009200401UL,
            0x128110040c84a84UL,
            0x81488020022802UL,
            0x8c0014100181UL,
            0x2222013020082UL,
            0xa00100002382c03UL,
            0x1000280001005c02UL,
            0x84801010000114cUL,
            0x480410048000084UL,
            0x21204420080020aUL,
            0x2020010000424a10UL,
            0x240041021d500141UL,
            0x420844000280214UL,
            0x29084a280042108UL,
            0x84102a8080a20a49UL,
            0x104204908010212UL,
            0x40a20280081860c1UL,
            0x3044000200121004UL,
            0x1001008807081122UL,
            0x50066c000210811UL,
            0xe3001240f8a106UL,
            0x940c0204030020d4UL,
            0x619204000210826aUL,
            0x2010438002b00a2UL,
            0x884042004005802UL,
            0xa90240000006404UL,
            0x500d082244010008UL,
            0x28190d00040014e0UL,
            0x825201600c082444UL,
        };

        private static readonly Bitboard[] RookMagics = {
            0x2080020500400f0UL,
            0x28444000400010UL,
            0x20000a1004100014UL,
            0x20010c090202006UL,
            0x8408008200810004UL,
            0x1746000808002UL,
            0x2200098000808201UL,
            0x12c0002080200041UL,
            0x104000208e480804UL,
            0x8084014008281008UL,
            0x4200810910500410UL,
            0x100014481c20400cUL,
            0x4014a4040020808UL,
            0x401002001010a4UL,
            0x202000500010001UL,
            0x8112808005810081UL,
            0x40902108802020UL,
            0x42002101008101UL,
            0x459442200810c202UL,
            0x81001103309808UL,
            0x8110000080102UL,
            0x8812806008080404UL,
            0x104020000800101UL,
            0x40a1048000028201UL,
            0x4100ba0000004081UL,
            0x44803a4003400109UL,
            0xa010a00000030443UL,
            0x91021a000100409UL,
            0x4201e8040880a012UL,
            0x22a000440201802UL,
            0x30890a72000204UL,
            0x10411402a0c482UL,
            0x40004841102088UL,
            0x40230000100040UL,
            0x40100010000a0488UL,
            0x1410100200050844UL,
            0x100090808508411UL,
            0x1410040024001142UL,
            0x8840018001214002UL,
            0x410201000098001UL,
            0x8400802120088848UL,
            0x2060080000021004UL,
            0x82101002000d0022UL,
            0x1001101001008241UL,
            0x9040411808040102UL,
            0x600800480009042UL,
            0x1a020000040205UL,
            0x4200404040505199UL,
            0x2020081040080080UL,
            0x40a3002000544108UL,
            0x4501100800148402UL,
            0x81440280100224UL,
            0x88008000000804UL,
            0x8084060000002812UL,
            0x1840201000108312UL,
            0x5080202000000141UL,
            0x1042a180880281UL,
            0x900802900c01040UL,
            0x8205104104120UL,
            0x9004220000440aUL,
            0x8029510200708UL,
            0x8008440100404241UL,
            0x2420001111000bdUL,
            0x4000882304000041UL,
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
                var occupancy = StaticInfo.GetAttackBitboard(Piece.WhiteBishop, source) & InnerSquares;

                int numBits = occupancy.CountSetBits();
                Debug.Assert(numBits <= 11);
                int shift = 64 - numBits;
                int numValues = 1 << numBits;
                BishopShifts[i] = shift;
                BishopTables[i] = new Bitboard[numValues];

                foreach (var blockers in occupancy.PowerSet())
                {
                    if (source == Location.Parse("e4") && blockers.NextLocation() == Location.Parse("c2")) Debugger.Break();

                    ushort key = (ushort)((blockers * BishopMagics[i]) >> shift);
                    // todo: assert that key has the same bit count as blockers. this doesn't appear to be happening.
                    BishopTables[i][key] = ComputeBishopAttacks(source, blockers);
                }
            }

            for (int i = 0; i < Location.NumberOfValues; i++)
            {
                var source = new Location((byte)i);
                var occupancy = StaticInfo.GetAttackBitboard(Piece.WhiteRook, source) & InnerSquares;

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
            var occupancy = attacks & InnerSquares;
            var blockers = occupancy & occupied;
            int index = source.Value;

            ushort key = (ushort)((blockers * BishopMagics[index]) >> BishopShifts[index]);
            return BishopTables[index][key];
        }

        public static Bitboard RookAttacks(Bitboard attacks, Bitboard occupied, Location source)
        {
            var occupancy = attacks & InnerSquares;
            var blockers = occupancy & occupied;
            int index = source.Value;

            ushort key = (ushort)((blockers * RookMagics[index]) >> RookShifts[index]);
            return RookTables[index][key];
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
            Debug.Assert(!blockers.OverlapsWith(OuterSquares));

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
            Debug.Assert(!blockers.OverlapsWith(OuterSquares));

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
