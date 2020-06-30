using ChessBot.Types;
using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// Helper class to evaluate state once the depth limit is reached or a terminal state is found.
    /// </summary>
    internal static class Evaluation
    {
        public const int MinScore = -int.MaxValue; // not int.MinValue because it can't be negated
        public const int MaxScore = int.MaxValue;

        private static readonly int[] PieceValues =
        {
            100,   // pawn
            320,   // knight
            330,   // bishop
            500,   // rook
            900,   // queen
            20000  // king
        };

        private static readonly int[][] PsqValues =
        {
            // pawn
            new int[]
            {
                 0,  0,  0,  0,  0,  0,  0,  0,
                50, 50, 50, 50, 50, 50, 50, 50,
                10, 10, 20, 30, 30, 20, 10, 10,
                 5,  5, 10, 25, 25, 10,  5,  5,
                 0,  0,  0, 20, 20,  0,  0,  0,
                 5, -5,-10,  0,  0,-10, -5,  5,
                 5, 10, 10,-20,-20, 10, 10,  5,
                 0,  0,  0,  0,  0,  0,  0,  0
            },
            // knight
            new int[]
            {
                -50,-40,-30,-30,-30,-30,-40,-50,
                -40,-20,  0,  0,  0,  0,-20,-40,
                -30,  0, 10, 15, 15, 10,  0,-30,
                -30,  5, 15, 20, 20, 15,  5,-30,
                -30,  0, 15, 20, 20, 15,  0,-30,
                -30,  5, 10, 15, 15, 10,  5,-30,
                -40,-20,  0,  5,  5,  0,-20,-40,
                -50,-40,-30,-30,-30,-30,-40,-50,
            },
            // bishop
            new int[]
            {
                -20,-10,-10,-10,-10,-10,-10,-20,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -10,  0,  5, 10, 10,  5,  0,-10,
                -10,  5,  5, 10, 10,  5,  5,-10,
                -10,  0, 10, 10, 10, 10,  0,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10,  5,  0,  0,  0,  0,  5,-10,
                -20,-10,-10,-10,-10,-10,-10,-20,
            },
            // rook
            new int[]
            {
                 0,  0,  0,  0,  0,  0,  0,  0,
                 5, 10, 10, 10, 10, 10, 10,  5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                 0,  0,  0,  5,  5,  0,  0,  0
            },
            // queen
            new int[]
            {
                -20,-10,-10, -5, -5,-10,-10,-20,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -10,  0,  5,  5,  5,  5,  0,-10,
                 -5,  0,  5,  5,  5,  5,  0, -5,
                  0,  0,  5,  5,  5,  5,  0, -5,
                -10,  5,  5,  5,  5,  5,  0,-10,
                -10,  0,  5,  0,  0,  0,  0,-10,
                -20,-10,-10, -5, -5,-10,-10,-20
            },
            // king
            // todo: use separate middlegame / endgame tables
            new int[]
            {
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -20,-30,-30,-40,-40,-30,-30,-20,
                -10,-20,-20,-20,-20,-20,-20,-10,
                 20, 20,  0,  0,  0,  0, 20, 20,
                 20, 30, 10,  0,  0, 10, 30, 20
            }
        };

        // note: these functions are always positive regardless of side

        public static int PieceScore(PieceKind kind) => PieceValues[(int)kind];
        
        public static int PsqScore(Piece piece, Location location)
        {
            var (file, rank) = location;
            int locationIndex = 8 * (piece.IsWhite ? (7 - (int)rank) : (int)rank) + (int)file;
            return PsqValues[(int)piece.Kind][locationIndex];
        }

        // note: Heuristic is calculated from the active player's viewpoint
        public static int Heuristic(MutState state)
        {
            int result = 0;
            for (var bb = state.Occupied; !bb.IsZero; bb = bb.ClearNext())
            {
                var location = bb.NextLocation();
                var piece = state.Board[location].Piece;
                var (kind, side) = (piece.Kind, piece.Side);

                int pieceValue = PieceScore(kind);
                int pieceSquareValue = PsqScore(piece, location);

                if (side == state.ActiveSide)
                {
                    result += pieceValue;
                    result += pieceSquareValue;
                }
                else
                {
                    result -= pieceValue;
                    result -= pieceSquareValue;
                }
            }

            return result;
        }

        public static int OfTerminal(MutState state)
        {
            // this assert is pretty costly
            //Debug.Assert(state.ToImmutable().IsTerminal);

            bool isStalemate = !state.IsCheck;
            if (isStalemate) return 0;
            return MinScore;
        }
    }
}
