using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static ChessBot.ChessPiece;

namespace ChessBot
{
    /// <summary>
    /// Immutable class representing the state of the chess board.
    /// </summary>
    public class ChessState
    {
        private static ChessState _initial;
        public static ChessState Initial
        {
            get
            {
                if (_initial == null)
                {
                    var pieceMap = new Dictionary<BoardLocation, ChessPiece>
                    {
                        [(0, 0)] = WhiteRook,
                        [(0, 1)] = WhiteKnight,
                        [(0, 2)] = WhiteBishop,
                        [(0, 3)] = WhiteQueen,
                        [(0, 4)] = WhiteKing,
                        [(0, 5)] = WhiteBishop,
                        [(0, 6)] = WhiteKnight,
                        [(0, 7)] = WhiteRook,

                        [(1, 0)] = WhitePawn,
                        [(1, 1)] = WhitePawn,
                        [(1, 2)] = WhitePawn,
                        [(1, 3)] = WhitePawn,
                        [(1, 4)] = WhitePawn,
                        [(1, 5)] = WhitePawn,
                        [(1, 6)] = WhitePawn,
                        [(1, 7)] = WhitePawn,

                        [(6, 0)] = BlackPawn,
                        [(6, 1)] = BlackPawn,
                        [(6, 2)] = BlackPawn,
                        [(6, 3)] = BlackPawn,
                        [(6, 4)] = BlackPawn,
                        [(6, 5)] = BlackPawn,
                        [(6, 6)] = BlackPawn,
                        [(6, 7)] = BlackPawn,

                        [(7, 0)] = BlackRook,
                        [(7, 1)] = BlackKnight,
                        [(7, 2)] = BlackBishop,
                        [(7, 3)] = BlackQueen,
                        [(7, 4)] = BlackKing,
                        [(7, 5)] = BlackBishop,
                        [(7, 6)] = BlackKnight,
                        [(7, 7)] = BlackRook,
                    };
                    _initial = new ChessState(pieceMap);
                }
                return _initial;
            }
        }

        private readonly ChessTile[,] _board; // todo: this should use an immutable array?

        public ChessState(
            // todo: implement iequatable
            IDictionary<BoardLocation, ChessPiece> pieceMap = null,
            PlayerColor nextPlayer = PlayerColor.White,
            bool hasWhiteCastled = false,
            bool hasBlackCastled = false)
        {
            _board = new ChessTile[8, 8];

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var location = (r, c);
                    _board[r, c] = (pieceMap != null && pieceMap.TryGetValue(location, out var piece))
                        ? new ChessTile(location, piece)
                        : new ChessTile(location);
                }
            }

            NextPlayer = nextPlayer;
            HasWhiteCastled = hasWhiteCastled;
            HasBlackCastled = hasBlackCastled;
        }

        public PlayerColor NextPlayer { get; }
        public bool HasWhiteCastled { get; }
        public bool HasBlackCastled { get; }

        public bool IsCheck => throw new NotImplementedException();
        public bool IsCheckmate => throw new NotImplementedException();
        public bool IsStalemate => throw new NotImplementedException();

        public ChessTile this[int row, int column] => _board[row, column];

        public ChessState ApplyMove(ChessMove move)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ChessState> GetSucessors()
        {
            throw new NotImplementedException();
        }
    }
}
