using ChessBot.Types;
using System;
using System.Diagnostics;

namespace ChessBot.Helpers
{
    /// <summary>
    /// Stores keys for use with Zobrist hashing.
    /// </summary>
    internal static class ZobristKey
    {
        private const int RngSeed = 0;
        private static readonly Random Rng = new Random(RngSeed);

        private static readonly ulong[,] PsqKeys = new ulong[12, 64];
        private static readonly ulong[] ActiveSideKeys = new ulong[2];
        private static readonly ulong[] CastlingRightsKeys = new ulong[16];
        private static readonly ulong[] EnPassantFileKeys = new ulong[8];

        static ZobristKey()
        {
            for (int i = 0; i < PsqKeys.GetLength(0); i++)
            {
                for (int j = 0; j < PsqKeys.GetLength(1); j++)
                {
                    PsqKeys[i, j] = Rng.NextUlong();
                }
            }

            for (int i = 0; i < ActiveSideKeys.Length; i++) ActiveSideKeys[i] = Rng.NextUlong();
            for (int i = 0; i < CastlingRightsKeys.Length; i++) CastlingRightsKeys[i] = Rng.NextUlong();
            for (int i = 0; i < EnPassantFileKeys.Length; i++) EnPassantFileKeys[i] = Rng.NextUlong();
        }

        public static ulong ForPsq(Piece piece, Location location)
        {
            Debug.Assert(piece.IsValid);
            Debug.Assert(location.IsValid);
            var (i, j) = (piece.ToIndex(), location.Value);
            return PsqKeys[i, j];
        }

        public static ulong ForActiveSide(Side side)
        {
            Debug.Assert(side.IsValid());
            return ActiveSideKeys[(int)side];
        }

        public static ulong ForCastlingRights(CastlingRights castlingRights)
        {
            Debug.Assert(castlingRights.IsValid());
            return CastlingRightsKeys[(int)castlingRights];
        }

        public static ulong ForEnPassantFile(File file)
        {
            Debug.Assert(file.IsValid());
            return EnPassantFileKeys[(int)file];
        }
    }
}
