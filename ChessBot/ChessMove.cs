using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using ChessBot.AlgebraicNotation;
using static ChessBot.AlgebraicNotation.AlgebraicNotationParser;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace ChessBot
{
    public class ChessMove
    {
        private static readonly Dictionary<string, PieceKind> _pieceKindMap = new Dictionary<string, PieceKind>
        {
            ["N"] = PieceKind.Knight,
            ["B"] = PieceKind.Bishop,
            ["R"] = PieceKind.Rook,
            ["Q"] = PieceKind.Queen,
            ["K"] = PieceKind.King,
        };

        private static MoveContext ParseInternal(string algebraicNotation)
        {
            var inputStream = new AntlrInputStream(algebraicNotation);
            var lexer = new AlgebraicNotationLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new AlgebraicNotationParser(tokenStream);
            return parser.move();
        }

        private static bool CanMoveFrom(
            ChessPiece piece,
            BoardLocation source,
            BoardLocation destination,
            bool isCapture)
        {
            if (source == destination)
            {
                return false;
            }

            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);
            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    return Math.Abs(delta.x) == Math.Abs(delta.y);
                case PieceKind.King:
                    // note: We ignore the possibility of castling since we already have logic to handle that
                    return Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1;
                case PieceKind.Knight:
                    return (Math.Abs(delta.x) == 1 && Math.Abs(delta.y) == 2) || (Math.Abs(delta.x) == 2 && Math.Abs(delta.y) == 1);
                case PieceKind.Pawn:
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    return isCapture
                        ? (Math.Abs(delta.x) == 1 && delta.y == forward)
                        : (delta.x == 0 && (delta.y == forward || delta.y == forward * 2));
                case PieceKind.Queen:
                    return delta.x == 0 || delta.y == 0 || Math.Abs(delta.x) == Math.Abs(delta.y);
                case PieceKind.Rook:
                    return delta.x == 0 || delta.y == 0;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static BoardLocation InferSourceLocation(
            SourceContext sourceNode,
            ChessState state,
            PieceKind pieceKind,
            BoardLocation destination,
            bool isCapture)
        {
            var possibleSources = state.IterateTiles();
            var sourceSquareNode = sourceNode?.square();
            var sourceFileNode = sourceNode?.FILE();
            var sourceRankNode = sourceNode?.RANK();

            if (sourceSquareNode != null)
            {
                var sourceLocation = BoardLocation.Parse(sourceSquareNode.GetText());
                possibleSources = new[] { state[sourceLocation] };
            }
            else if (sourceFileNode != null)
            {
                int sourceColumn = sourceFileNode.GetText()[0] - 'a';
                possibleSources = possibleSources.Where(t => t.Location.Column == sourceColumn);
            }
            else if (sourceRankNode != null)
            {
                int sourceRow = sourceRankNode.GetText()[0] - '1';
                possibleSources = possibleSources.Where(t => t.Location.Row == sourceRow);
            }

            var sourceTile = possibleSources.Single(t => t.HasPiece && t.Piece.Kind == pieceKind && CanMoveFrom(t.Piece, t.Location, destination, isCapture));
            return sourceTile.Location;
        }

        // note: This method only checks that the specified piece occupies the source square.
        // It doesn't actually check whether the move is valid; that's done in ChessState.ApplyMove.
        public static ChessMove Parse(string algebraicNotation, ChessState state)
        {
            var moveNode = ParseInternal(algebraicNotation);
            if (moveNode.exception != null)
            {
                throw new AlgebraicNotationParseException("Could not parse input", moveNode.exception);
            }

            // todo: enforce check/checkmate if they are specified
            var moveDescNode = moveNode.moveDesc();
            var kingsideCastleNode = moveDescNode.KINGSIDE_CASTLE();
            var queensideCastleNode = moveDescNode.QUEENSIDE_CASTLE();
            if (kingsideCastleNode != null || queensideCastleNode != null)
            {
                var kingsTile = state.IterateTiles().Single(
                    t => t.HasPiece && t.Piece.Kind == PieceKind.King && t.Piece.Color == state.NextPlayer);
                var source = kingsTile.Location;
                var destination = (kingsideCastleNode != null) ? source.Right(2) : source.Left(2);
                return new ChessMove(
                    source,
                    destination,
                    isKingsideCastle: (kingsideCastleNode != null),
                    isQueensideCastle: (queensideCastleNode != null));
            }
            else
            {
                var ordinaryMoveDescNode = moveDescNode.ordinaryMoveDesc();
                var pieceKindNode = ordinaryMoveDescNode.pieceKind();
                var sourceNode = ordinaryMoveDescNode.source();
                var captureNode = ordinaryMoveDescNode.CAPTURE();
                var destinationNode = ordinaryMoveDescNode.destination();
                var promotionKindNode = ordinaryMoveDescNode.promotionKind();

                var pieceKind = (pieceKindNode != null) ? _pieceKindMap[pieceKindNode.GetText()] : PieceKind.Pawn;
                bool isCapture = (captureNode != null);
                var destination = BoardLocation.Parse(destinationNode.GetText());
                var promotionKind = (promotionKindNode != null) ? _pieceKindMap[promotionKindNode.GetText()] : (PieceKind?)null;
                var source = InferSourceLocation(sourceNode, state, pieceKind, destination, isCapture);

                return new ChessMove(
                    source,
                    destination,
                    isCapture: isCapture,
                    promotionKind: promotionKind);
            }
        }

        public ChessMove(
            BoardLocation source,
            BoardLocation destination,
            bool isCapture = false,
            bool isKingsideCastle = false,
            bool isQueensideCastle = false,
            PieceKind? promotionKind = null)
        {
            if (source == destination)
            {
                // todo: throw error
            }

            if ((isKingsideCastle && destination != source.Right(2)) ||
                (isQueensideCastle && destination != source.Left(2)))
            {
                // todo: throw error
            }

            switch (promotionKind)
            {
                case null:
                case PieceKind.Bishop:
                case PieceKind.Knight:
                case PieceKind.Queen:
                case PieceKind.Rook:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(promotionKind));
            }

            Source = source;
            Destination = destination;
            IsCapture = isCapture;
            IsKingsideCastle = isKingsideCastle;
            IsQueensideCastle = isQueensideCastle;
            PromotionKind = promotionKind;
        }

        public BoardLocation Source { get; }
        public BoardLocation Destination { get; }
        public bool IsCapture { get; }
        public bool IsKingsideCastle { get; }
        public bool IsQueensideCastle { get; }
        public PieceKind? PromotionKind { get; }

        public override string ToString()
        {
            return $"{Source} > {Destination}";
        }
    }
}
