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
                    var pieceMap = new Dictionary<string, ChessPiece>
                    {
                        ["a1"] = WhiteRook,
                        ["b1"] = WhiteKnight,
                        ["c1"] = WhiteBishop,
                        ["d1"] = WhiteQueen,
                        ["e1"] = WhiteKing,
                        ["f1"] = WhiteBishop,
                        ["g1"] = WhiteKnight,
                        ["h1"] = WhiteRook,

                        ["a2"] = WhitePawn,
                        ["b2"] = WhitePawn,
                        ["c2"] = WhitePawn,
                        ["d2"] = WhitePawn,
                        ["e2"] = WhitePawn,
                        ["f2"] = WhitePawn,
                        ["g2"] = WhitePawn,
                        ["h2"] = WhitePawn,

                        ["a7"] = BlackPawn,
                        ["b7"] = BlackPawn,
                        ["c7"] = BlackPawn,
                        ["d7"] = BlackPawn,
                        ["e7"] = BlackPawn,
                        ["f7"] = BlackPawn,
                        ["g7"] = BlackPawn,
                        ["h7"] = BlackPawn,

                        ["a8"] = BlackRook,
                        ["b8"] = BlackKnight,
                        ["c8"] = BlackBishop,
                        ["d8"] = BlackQueen,
                        ["e8"] = BlackKing,
                        ["f8"] = BlackBishop,
                        ["g8"] = BlackKnight,
                        ["h8"] = BlackRook,
                    };
                    _initial = new ChessState(pieceMap);
                }
                return _initial;
            }
        }

        private static ChessTile[,] CreateBoard(IDictionary<string, ChessPiece> pieceMap)
        {
            var board = new ChessTile[8, 8];

            foreach (var (locationString, piece) in pieceMap)
            {
                var (c, r) = BoardLocation.Parse(locationString);
                board[c, r] = new ChessTile((c, r), piece);
            }

            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    if (board[c, r] == null)
                    {
                        board[c, r] = new ChessTile((c, r));
                    }
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
            IDictionary<string, ChessPiece> pieceMap = null,
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
