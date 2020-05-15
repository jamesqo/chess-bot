using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using ChessBot.AlgebraicNotation;
using static ChessBot.AlgebraicNotation.AlgebraicNotationParser;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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

        // Returns the tiles along a vertical, horizontal, or diagonal line between
        // `source` and `destination`, exclusive.
        private static IEnumerable<BoardLocation> GetLocationsBetween(
            BoardLocation source,
            BoardLocation destination)
        {
            Debug.Assert(source != destination);
            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);

            if (delta.x == 0)
            {
                // Vertical
                var start = (delta.y > 0) ? source : destination;
                int shift = Math.Abs(delta.y);
                for (int dy = 1; dy < shift; dy++)
                {
                    yield return start.Up(dy);
                }
            }
            else if (delta.y == 0)
            {
                // Horizontal
                var start = (delta.x > 0) ? source : destination;
                int shift = Math.Abs(delta.x);
                for (int dx = 1; dx < shift; dx++)
                {
                    yield return start.Right(dx);
                }
            }
            else
            {
                // Diagonal
                Debug.Assert(Math.Abs(delta.x) == Math.Abs(delta.y));

                var start = (delta.x > 0) ? source : destination;
                int shift = Math.Abs(delta.x);
                int slope = (delta.x == delta.y) ? 1 : -1;
                for (int dx = 1; dx < shift; dx++)
                {
                    int dy = dx * slope;
                    yield return start.Right(dx).Up(dy);
                }
            }
        }

        // Checks whether it's possible to move the piece on `source` to `destination`.
        // Ignores whether we would create an illegal position by, for example, putting our king in check.
        private static bool IsMovePossible(
            ChessState state,
            BoardLocation source,
            BoardLocation destination)
        {
            if (source == destination)
            {
                return false;
            }

            var sourceTile = state[source];
            var destinationTile = state[destination];
            var piece = sourceTile.Piece;

            if (destinationTile.HasPiece && destinationTile.Piece.Color == piece.Color)
            {
                return false;
            }

            bool canMoveIfUnblocked;
            bool canPieceBeBlocked = false;
            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == Math.Abs(delta.y));
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.King:
                    // note: We ignore the possibility of castling since we already have logic to handle that
                    canMoveIfUnblocked = (Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1);
                    break;
                case PieceKind.Knight:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == 1 && Math.Abs(delta.y) == 2) || (Math.Abs(delta.x) == 2 && Math.Abs(delta.y) == 1);
                    break;
                case PieceKind.Pawn:
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    bool isAdvance = (!destinationTile.HasPiece && delta.x == 0 && (delta.y == forward || delta.y == forward * 2));
                    bool isCapture = (destinationTile.HasPiece && Math.Abs(delta.x) == 1 && delta.y == forward); // todo: support en passant captures

                    canMoveIfUnblocked = (isAdvance || isCapture);
                    canPieceBeBlocked = isAdvance;
                    break;
                case PieceKind.Queen:
                    canMoveIfUnblocked = (delta.x == 0 || delta.y == 0 || Math.Abs(delta.x) == Math.Abs(delta.y));
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.Rook:
                    canMoveIfUnblocked = (delta.x == 0 || delta.y == 0);
                    canPieceBeBlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return canMoveIfUnblocked && (!canPieceBeBlocked || GetLocationsBetween(source, destination).All(loc => !state[loc].HasPiece));
        }

        private static BoardLocation InferSourceLocation(
            SourceContext sourceNode,
            ChessState state,
            PieceKind pieceKind,
            BoardLocation destination)
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

            var sourceTile = possibleSources.Single(t => t.HasPiece && t.Piece.Kind == pieceKind && IsMovePossible(state, t.Location, destination));
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
                bool isCapture = (captureNode != null); // todo: enforce this. take en passant captures into account.
                var destination = BoardLocation.Parse(destinationNode.GetText());
                var promotionKind = (promotionKindNode != null) ? _pieceKindMap[promotionKindNode.GetText()] : (PieceKind?)null;
                var source = InferSourceLocation(sourceNode, state, pieceKind, destination);

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
            // todo: add more fields
            return $"{Source} > {Destination}";
        }
    }
}
