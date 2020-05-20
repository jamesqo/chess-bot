using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using ChessBot.AlgebraicNotation;
using static ChessBot.AlgebraicNotation.AlgebraicNotationParser;
using System.Linq;

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

        internal static ChessMove Castle(PlayerColor color, bool kingside)
        {
            var source = BoardLocation.Parse(color == PlayerColor.White ? "e1" : "e8");
            var destination = kingside ? source.Right(2) : source.Left(2);
            return new ChessMove(source, destination, isKingsideCastle: kingside, isQueensideCastle: !kingside)
        }

        private static MoveContext ParseInternal(string algebraicNotation)
        {
            var inputStream = new AntlrInputStream(algebraicNotation);
            var lexer = new AlgebraicNotationLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new AlgebraicNotationParser(tokenStream);
            return parser.move();
        }

        private static BoardLocation InferSource(
            SourceContext sourceNode,
            ChessState state,
            PieceKind pieceKind,
            BoardLocation destination)
        {
            var possibleSources = state.ActivePlayer.GetOccupiedTiles();
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

            try
            {
                var sourceTile = possibleSources.Single(t => t.Piece.Kind == pieceKind && state.IsMovePossible(t.Location, destination));
                return sourceTile.Location;
            }
            catch (InvalidOperationException e)
            {
                throw new AlgebraicNotationParseException("Could not infer source location", e);
            }
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
                // todo: add a test for when we try to castle but there's no king / multiple kings
                var source = state.GetKingsLocation(state.ActiveColor) ?? throw new AlgebraicNotationParseException("Attempt to castle without exactly 1 king");
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
                bool isCapture = (captureNode != null); // todo: enforce this if true. take en passant captures into account.
                var destination = BoardLocation.Parse(destinationNode.GetText());
                var promotionKind = (promotionKindNode != null) ? _pieceKindMap[promotionKindNode.GetText()] : (PieceKind?)null;
                var source = InferSource(sourceNode, state, pieceKind, destination);

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
            // todo: add info about more fields
            return $"{Source} > {Destination}";
        }
    }
}
