using ChessBot.Types;
using System;
using System.Diagnostics;
using static System.Convert;

namespace ChessBot.Utils
{
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
            int i = ((int)piece.Side * 6) + (int)piece.Kind;
            int j = location.Value;
            return PieceSquareKeys[i, j];
        }

        public static ulong ForActiveSide(Side side)
        {
            Debug.Assert(side.IsValid());
            return ActiveSideKeys[(int)side];
        }

        // todo: use an enum here and get rid of PlayerState in the future
        public static ulong ForCastlingRights(bool K, bool Q, bool k, bool q)
        {
            int flags = ToInt32(K) | (ToInt32(Q) << 1) | (ToInt32(k) << 2) | (ToInt32(q) << 3);
            return CastlingRightsKeys[flags];
        }

        public static ulong ForEnPassantFile(File file)
        {
            Debug.Assert(file.IsValid());
            return EnPassantFileKeys[(int)file];
        }
    }
}
