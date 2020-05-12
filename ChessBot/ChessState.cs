using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
                        [(1, 0)] = WhiteKnight,
                        [(2, 0)] = WhiteBishop,
                        [(3, 0)] = WhiteQueen,
                        [(4, 0)] = WhiteKing,
                        [(5, 0)] = WhiteBishop,
                        [(6, 0)] = WhiteKnight,
                        [(7, 0)] = WhiteRook,

                        [(0, 1)] = WhitePawn,
                        [(1, 1)] = WhitePawn,
                        [(2, 1)] = WhitePawn,
                        [(3, 1)] = WhitePawn,
                        [(4, 1)] = WhitePawn,
                        [(5, 1)] = WhitePawn,
                        [(6, 1)] = WhitePawn,
                        [(7, 1)] = WhitePawn,

                        [(0, 6)] = BlackPawn,
                        [(1, 6)] = BlackPawn,
                        [(2, 6)] = BlackPawn,
                        [(3, 6)] = BlackPawn,
                        [(4, 6)] = BlackPawn,
                        [(5, 6)] = BlackPawn,
                        [(6, 6)] = BlackPawn,
                        [(7, 6)] = BlackPawn,

                        [(0, 7)] = BlackRook,
                        [(1, 7)] = BlackKnight,
                        [(2, 7)] = BlackBishop,
                        [(3, 7)] = BlackQueen,
                        [(4, 7)] = BlackKing,
                        [(5, 7)] = BlackBishop,
                        [(6, 7)] = BlackKnight,
                        [(7, 7)] = BlackRook,
                    };
                    _initial = new ChessState(pieceMap);
                }
                return _initial;
            }
        }

        private static ChessTile[,] CreateBoard(IDictionary<BoardLocation, ChessPiece> pieceMap)
        {
            var board = new ChessTile[8, 8];
            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    var location = (c, r);
                    board[c, r] = (pieceMap != null && pieceMap.TryGetValue(location, out var piece))
                        ? new ChessTile(location, piece)
                        : new ChessTile(location);
                }
            }
            return board;
        }

        private readonly ChessTile[,] _board; // todo: this should use an immutable array?

        private ChessState(
            ChessTile[,] board,
            PlayerColor nextPlayer,
            bool hasWhiteCastled,
            bool hasBlackCastled)
        {
            _board = board;
            NextPlayer = nextPlayer;
            HasWhiteCastled = hasWhiteCastled;
            HasBlackCastled = hasBlackCastled;
        }

        public ChessState(
            IDictionary<BoardLocation, ChessPiece> pieceMap = null,
            PlayerColor nextPlayer = PlayerColor.White,
            bool hasWhiteCastled = false,
            bool hasBlackCastled = false)
            : this(CreateBoard(pieceMap), nextPlayer, hasWhiteCastled, hasBlackCastled)
        {
        }

        public PlayerColor NextPlayer { get; }
        public bool HasWhiteCastled { get; }
        public bool HasBlackCastled { get; }

        public bool IsCheck => false; // todo
        public bool IsCheckmate => false; // todo
        public bool IsStalemate => false; // todo

        public ChessTile this[int column, int row] => _board[column, row];
        public ChessTile this[BoardLocation location] => this[location.Column, location.Row];

        public ChessState ApplyMove(ChessMove move)
        {
            // todo: verify lots of stuff

            var (source, destination) = (move.Source, move.Destination);
            var piece = this[source].Piece;

            var newBoard = (ChessTile[,])_board.Clone();
            var (sx, sy, dx, dy) = (source.Column, source.Row, destination.Column, destination.Row);
            newBoard[sx, sy] = this[source].WithPiece(null);
            newBoard[dx, dy] = this[destination].WithPiece(piece);

            var newNextPlayer = (NextPlayer == PlayerColor.White) ? PlayerColor.Black : PlayerColor.White;

            bool castled = (piece.Kind == PieceKind.King && (destination == source.Right(2) || destination == source.Left(2)));
            bool newHasWhiteCastled = HasWhiteCastled || (NextPlayer == PlayerColor.White && castled);
            bool newHasBlackCastled = HasBlackCastled || (NextPlayer == PlayerColor.Black && castled);

            return new ChessState(
                newBoard,
                newNextPlayer,
                newHasWhiteCastled,
                newHasBlackCastled);
        }

        public IEnumerable<ChessState> GetSucessors()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ChessTile> IterateTiles()
        {
            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    yield return this[c, r];
                }
            }
        }
    }
}
