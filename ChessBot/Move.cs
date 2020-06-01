using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using ChessBot.AlgebraicNotation;
using System.Linq;
using ChessBot.Exceptions;
using System.Diagnostics.CodeAnalysis;
using ChessBot.Types;
using static ChessBot.AlgebraicNotation.AlgebraicNotationParser;

namespace ChessBot
{
    public class Move : IEquatable<Move>
    {
        private static readonly Dictionary<string, PieceKind> s_pieceKindMap = new Dictionary<string, PieceKind>
        {
            ["N"] = PieceKind.Knight,
            ["B"] = PieceKind.Bishop,
            ["R"] = PieceKind.Rook,
            ["Q"] = PieceKind.Queen,
            ["K"] = PieceKind.King,
        };

        // todo: this is misplaced
        internal static Move Castle(Side side, bool kingside)
        {
            var source = new Location(File.FileE, side.IsWhite() ? Rank.Rank1 : Rank.Rank8);
            var destination = kingside ? source.Right(2) : source.Left(2);
            return new Move(source, destination, isKingsideCastle: kingside, isQueensideCastle: !kingside);
        }

        private static MoveContext ParseInternal(string an)
        {
            var inputStream = new AntlrInputStream(an);
            var lexer = new AlgebraicNotationLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new AlgebraicNotationParser(tokenStream);
            return parser.move();
        }

        private static Location InferSource(
            SourceContext sourceNode,
            State state,
            PieceKind pieceKind,
            Location destination)
        {
            var possibleSources = state.ActivePlayer.GetOccupiedTiles().AsEnumerable();
            var sourceSquareNode = sourceNode?.square();
            var sourceFileNode = sourceNode?.FILE();
            var sourceRankNode = sourceNode?.RANK();

            if (sourceSquareNode != null)
            {
                var sourceLocation = Location.Parse(sourceSquareNode.GetText());
                possibleSources = new[] { state[sourceLocation] };
            }
            else if (sourceFileNode != null)
            {
                var sourceFile = FileHelpers.FromChar(sourceFileNode.GetText()[0]);
                possibleSources = possibleSources.Where(t => t.Location.File == sourceFile);
            }
            else if (sourceRankNode != null)
            {
                var sourceRank = RankHelpers.FromChar(sourceRankNode.GetText()[0]);
                possibleSources = possibleSources.Where(t => t.Location.Rank == sourceRank);
            }

            try
            {
                var sourceTile = possibleSources.Single(t => t.Piece.Kind == pieceKind && state.IsMovePossible(t.Location, destination));
                return sourceTile.Location;
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidMoveException("Could not infer source location", e);
            }
        }

        // note: This method only checks that the specified piece occupies the source square.
        // It doesn't actually check whether the move is valid; that's done in ChessState.ApplyMove.
        public static Move Parse(string an, State state)
        {
            var moveNode = ParseInternal(an);
            // For some reason, the former is null for empty inputs
            var error = moveNode.exception ?? moveNode.moveDesc().exception;
            if (error != null)
            {
                throw new AnParseException("Could not parse input", error);
            }

            var statusNode = moveNode.status();
            var checkNode = statusNode?.CHECK();
            var checkmateNode = statusNode?.CHECKMATE();
            bool isCheck = (checkNode != null);
            bool isCheckmate = (checkmateNode != null);

            var moveDescNode = moveNode.moveDesc();
            var kingsideCastleNode = moveDescNode.KINGSIDE_CASTLE();
            var queensideCastleNode = moveDescNode.QUEENSIDE_CASTLE();
            if (kingsideCastleNode != null || queensideCastleNode != null)
            {
                // todo: add a test for when we try to castle but there's no king / multiple kings
                var source = state.GetKingsLocation(state.ActiveSide) ?? throw new InvalidMoveException("Attempt to castle without exactly 1 king");
                var destination = (kingsideCastleNode != null) ? source.Right(2) : source.Left(2);
                return new Move(
                    source,
                    destination,
                    isKingsideCastle: (kingsideCastleNode != null),
                    isQueensideCastle: (queensideCastleNode != null),
                    isCapture: false,
                    isCheck: isCheck,
                    isCheckmate: isCheckmate
                    );
            }
            else
            {
                var ordinaryMoveDescNode = moveDescNode.ordinaryMoveDesc();
                var pieceKindNode = ordinaryMoveDescNode.pieceKind();
                var sourceNode = ordinaryMoveDescNode.source();
                var captureNode = ordinaryMoveDescNode.CAPTURE();
                var destinationNode = ordinaryMoveDescNode.destination();
                var promotionKindNode = ordinaryMoveDescNode.promotionKind();

                var pieceKind = (pieceKindNode != null) ? s_pieceKindMap[pieceKindNode.GetText()] : PieceKind.Pawn;
                bool isCapture = (captureNode != null);
                var destination = Location.Parse(destinationNode.GetText());
                var promotionKind = (promotionKindNode != null) ? s_pieceKindMap[promotionKindNode.GetText()] : (PieceKind?)null;
                var source = InferSource(sourceNode, state, pieceKind, destination);

                return new Move(
                    source,
                    destination,
                    promotionKind: promotionKind,
                    isCapture: isCapture,
                    isCheck: isCheck,
                    isCheckmate: isCheckmate
                    );
            }
        }

        public Move(
            Location source,
            Location destination,
            bool isKingsideCastle = false,
            bool isQueensideCastle = false,
            PieceKind? promotionKind = null,
            bool? isCapture = null,
            bool? isCheck = null,
            bool? isCheckmate = null)
        {
            if (source == destination)
            {
                throw new InvalidMoveException("Source cannot be the same as the destination");
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
                    throw new InvalidMoveException($"Bad value for ${nameof(promotionKind)}");
            }

            Source = source;
            Destination = destination;
            IsKingsideCastle = isKingsideCastle;
            IsQueensideCastle = isQueensideCastle;
            PromotionKind = promotionKind;

            IsCapture = isCapture;
            IsCheck = isCheck;
            IsCheckmate = isCheckmate;
        }

        public Location Source { get; }
        public Location Destination { get; }
        public bool IsKingsideCastle { get; }
        public bool IsQueensideCastle { get; }
        public PieceKind? PromotionKind { get; }

        public bool? IsCapture { get; }
        public bool? IsCheck { get; }
        public bool? IsCheckmate { get; }

        public override bool Equals(object obj) => Equals(obj as Move);

        public bool Equals([AllowNull] Move other)
        {
            if (other == null) return false;
            return Source == other.Source
                && Destination == other.Destination
                && IsKingsideCastle == other.IsKingsideCastle
                && IsQueensideCastle == other.IsQueensideCastle
                && PromotionKind == other.PromotionKind
                // The only cases in which these 3 fields will affect the result is when they're specified for both objects, but differ
                && (!IsCapture.HasValue || !other.IsCapture.HasValue || IsCapture.Value == other.IsCapture.Value)
                && (!IsCheck.HasValue || !other.IsCheck.HasValue || IsCheck.Value == other.IsCheck.Value)
                && (!IsCheckmate.HasValue || !other.IsCheckmate.HasValue || IsCheckmate.Value == other.IsCheckmate.Value);
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.Add(Source);
            hc.Add(Destination);
            hc.Add(IsKingsideCastle);
            hc.Add(IsQueensideCastle);
            hc.Add(PromotionKind);
            // We exclude IsCapture / IsCheck / IsCheckmate intentionally
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            // todo: add info about more fields
            return $"{Source}{Destination}";
        }
    }
}
