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

        private static readonly ulong[,] PieceSquareKeys = new ulong[12, 64];
        private static readonly ulong[] ActiveSideKeys = new ulong[2];
        private static readonly ulong[] CastlingRightsKeys = new ulong[16];
        private static readonly ulong[] EnPassantFileKeys = new ulong[8];

        static ZobristKey()
        {
            for (int i = 0; i < PieceSquareKeys.GetLength(0); i++)
            {
                for (int j = 0; j < PieceSquareKeys.GetLength(1); j++)
                {
                    PieceSquareKeys[i, j] = Rng.NextULong();
                }
            }

            for (int i = 0; i < ActiveSideKeys.Length; i++) ActiveSideKeys[i] = Rng.NextULong();
            for (int i = 0; i < CastlingRightsKeys.Length; i++) CastlingRightsKeys[i] = Rng.NextULong();
            for (int i = 0; i < EnPassantFileKeys.Length; i++) EnPassantFileKeys[i] = Rng.NextULong();
        }

        public static ulong ForPieceSquare(Piece piece, Location location)
        {
            Debug.Assert(piece.IsValid);
            Debug.Assert(location.IsValid);
            var (i, j) = (piece.ToIndex(), location.Value);
            return PieceSquareKeys[i, j];
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
