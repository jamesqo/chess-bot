using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using ChessBot.AlgebraicNotation;
using System.Linq;
using ChessBot.Exceptions;
using static ChessBot.AlgebraicNotation.AlgebraicNotationParser;
using System.Text;

namespace ChessBot.Types
{
    /// <summary>
    /// Stores information about a chess move.
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        private static readonly Dictionary<string, PieceKind> s_pieceKindMap = new Dictionary<string, PieceKind>
        {
            ["N"] = PieceKind.Knight,
            ["B"] = PieceKind.Bishop,
            ["R"] = PieceKind.Rook,
            ["Q"] = PieceKind.Queen,
            ["K"] = PieceKind.King,
        };

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
            Location destination,
            PieceKind sourceKind,
            bool isCapture)
        {
            bool realIsCapture = state[destination].HasPiece || (sourceKind == PieceKind.Pawn && destination == state.EnPassantTarget);
            if (isCapture != realIsCapture)
            {
                throw new InvalidMoveException($"Incorrect value for {nameof(isCapture)} provided");
            }

            var possibleSources = state.ActivePlayer.GetOccupiedTiles();
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
                var sourceTile = possibleSources.Single(t => t.Piece.Kind == sourceKind
                    // Castling should be denoted with the special notation O-O / O-O-O, we don't want to accept Kg1 / Kc1
                    && state.IsMovePossible(t.Location, destination, allowCastling: false));
                return sourceTile.Location;
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidMoveException("Could not infer source location", e);
            }
        }

        // todo: we shouldn't be outputting stuff to console when antlr can't parse the input

        /// <summary>
        /// Parses a <see cref="Move"/> from algebraic notation and a <see cref="State"/> object.
        /// </summary>
        /// <remarks>
        /// For the most part, this method doesn't actually check whether the move is valid; that's done in <see cref="State.Apply(Move)"/>.
        /// </remarks>
        public static Move Parse(string an, State state)
        {
            var moveNode = ParseInternal(an);
            // For some reason, the former is null for empty inputs
            var error = moveNode.exception ?? moveNode.moveDesc().exception;
            if (error != null)
            {
                throw new AnParseException("Could not parse input", error);
            }

            //var statusNode = moveNode.status();
            //var checkNode = statusNode?.CHECK();
            //var checkmateNode = statusNode?.CHECKMATE();

            var moveDescNode = moveNode.moveDesc();
            var kingsideCastleNode = moveDescNode.KINGSIDE_CASTLE();
            var queensideCastleNode = moveDescNode.QUEENSIDE_CASTLE();
            if (kingsideCastleNode != null || queensideCastleNode != null)
            {
                // todo: add a test for when we try to castle but there's no king / multiple kings
                var source = state.FindKing(state.ActiveSide) ?? throw new InvalidMoveException("Attempt to castle without exactly 1 king");
                var destination = (kingsideCastleNode != null) ? source.Right(2) : source.Left(2);
                return new Move(source, destination);
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
                var source = InferSource(sourceNode, state, destination, pieceKind, isCapture);

                return new Move(source, destination, promotionKind: promotionKind);
            }
        }

        public Move(Location source, Location destination, PieceKind? promotionKind = null)
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

            int kindValue = promotionKind.HasValue ? ((int)promotionKind.Value + 1) : 0;
            _value = (ushort)(source.Value | (destination.Value << DestinationShift) | (kindValue << PromotionKindShift));
        }

        private readonly ushort _value;

        private const ushort SourceMask        = 0b0000_0000_0011_1111;
        private const ushort DestinationMask   = 0b0000_1111_1100_0000;
        private const ushort PromotionKindMask = 0b0111_0000_0000_0000;

        private const int DestinationShift = Location.NumberOfBits;
        private const int PromotionKindShift = Location.NumberOfBits * 2;

        public Location Source => new Location((byte)(_value & SourceMask));
        public Location Destination => new Location((byte)((_value & DestinationMask) >> DestinationShift));
        public PieceKind? PromotionKind
        {
            get
            {
                int kindValue = (_value & PromotionKindMask) >> PromotionKindShift;
                return kindValue == 0 ? (PieceKind?)null : (PieceKind)(kindValue - 1);
            }
        }

        internal bool IsDefault => _value == 0;
        internal bool IsValid => (Source.IsValid && Destination.IsValid && (PromotionKind?.IsValid() ?? true));

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public bool Equals(Move other) => _value == other._value;

        public override int GetHashCode() => _value;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Source);
            sb.Append(Destination);
            sb.Append(PromotionKind switch
            {
                PieceKind.Knight => "N",
                PieceKind.Bishop => "B",
                PieceKind.Rook => "R",
                PieceKind.Queen => "Q",
                null => ""
            });
            return sb.ToString();
        }
    }
}
