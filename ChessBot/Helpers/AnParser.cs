using ChessBot.Exceptions;
using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Linq;

namespace ChessBot.Helpers
{
    /// <summary>
    /// Helper class for parsing algebraic notation (AN) and long algebraic notation (LAN).
    /// </summary>
    internal class AnParser
    {
        private readonly string _input;
        private int _index = 0;

        public AnParser(string input)
        {
            _input = input;
        }

        private bool CanRead(int count) => _index + count <= _input.Length;
        private char Peek(int i = 0) => _input[_index + i];
        private char Pop() => _input[_index++];

        public Move Parse(State ctx)
        {
            Location source, destination;
            PieceKind? promotionKind;

            bool isKingsideCastle = (_input == "O-O" || _input == "0-0");
            bool isQueensideCastle = (_input == "O-O-O" || _input == "0-0-0");
            if (isKingsideCastle || isQueensideCastle)
            {
                source = StaticInfo.GetStartLocation(ctx.ActiveSide, PieceKind.King);
                destination = isKingsideCastle ? source.Right(2) : source.Left(2);
                return new Move(source, destination);
            }

            var sourceKind = ReadPieceKind() ?? PieceKind.Pawn;
            if (ReadLocation() is Location dest)
            {
                destination = dest;
                promotionKind = ReadPieceKind();
                ReadStatus();

                if (!CanRead(1))
                {
                    source = InferSource("", ctx, destination, sourceKind, isCapture: false);
                    return new Move(source, destination, promotionKind);
                }
            }

            var partialSource = ReadPartialLocation();
            bool isCapture = ReadSeparator();
            destination = StrictReadLocation();
            promotionKind = ReadPieceKind();
            ReadStatus();

            source = InferSource(partialSource, ctx, destination, sourceKind, isCapture);
            return new Move(source, destination, promotionKind);
        }

        // todo: gotta handle castling
        public Move ParseLong()
        {
            ReadPieceKind();
            var source = StrictReadLocation();
            ReadSeparator(true);
            var destination = StrictReadLocation();
            var promotionKind = ReadPieceKind();
            return new Move(source, destination, promotionKind);
        }

        private PieceKind? ReadPieceKind()
        {
            if (!CanRead(1)) return null;

            switch (Peek())
            {
                case 'N': Pop(); return PieceKind.Knight;
                case 'B': Pop(); return PieceKind.Bishop;
                case 'R': Pop(); return PieceKind.Rook;
                case 'Q': Pop(); return PieceKind.Queen;
                case 'K': Pop(); return PieceKind.King;
                default: return null;
            }
        }

        private Location? ReadLocation()
        {
            if (!CanRead(2)) return null;

            var (fileChar, rankChar) = (Peek(), Peek(1));
            if (fileChar < 'a' || fileChar > 'h') return null;
            if (rankChar < '1' || rankChar > '8') return null;

            return (FileHelpers.FromChar(Pop()), RankHelpers.FromChar(Pop()));
        }

        private Location StrictReadLocation() =>
            ReadLocation() is Location loc ? loc : throw new InvalidMoveException(InvalidMoveReason.BadAlgebraicNotation);

        private string ReadPartialLocation()
        {
            if (!CanRead(2)) throw new InvalidMoveException(InvalidMoveReason.BadAlgebraicNotation);

            string result = "";
            char next = Peek();

            if (next != 'x')
            {
                if ((next >= 'a' && next <= 'h') || (next >= '1' && next <= '8'))
                {
                    result = next.ToString();
                    Pop();

                    if (next >= 'a' && next <= 'h')
                    {
                        // we already decided that it wasn't a destination, so it could be something like "e8xd8" where both
                        // the source and destination are fully specified
                        next = Peek();
                        if (next >= '1' && next <= '8')
                        {
                            result += next;
                            Pop();
                        }
                    }
                }
            }

            return result;
        }

        private bool ReadSeparator(bool allowHyphen = false)
        {
            // there should always be something following the separator
            if (!CanRead(1)) throw new InvalidMoveException(InvalidMoveReason.BadAlgebraicNotation);

            char next = Peek();
            if ((allowHyphen && next == '-') || next == 'x')
            {
                Pop();
                return true;
            }
            return false;
        }

        private bool ReadStatus()
        {
            if (!CanRead(1)) return false;

            char next = Peek();
            if (next == '+' || next == '#')
            {
                Pop();
                return true;
            }
            return false;
        }

        private static Location InferSource(
            string partialSource,
            State ctx,
            Location destination,
            PieceKind sourceKind,
            bool isCapture)
        {
            Debug.Assert(partialSource.Length == 1 || partialSource.Length == 2);

            bool realIsCapture = ctx[destination].HasPiece || (sourceKind == PieceKind.Pawn && destination == ctx.EnPassantTarget);
            if (isCapture != realIsCapture)
            {
                throw new InvalidMoveException(InvalidMoveReason.BadCaptureNotation);
            }

            var possibleSources = ctx.ActivePlayer.GetOccupiedTiles();
            char? fileChar = (partialSource.Length == 1 && partialSource[0] >= 'a' && partialSource[0] <= 'h') ? partialSource[0] : (char?)null;
            char? rankChar = (partialSource.Length == 1 && partialSource[0] >= '1' && partialSource[0] <= '8') ? partialSource[0] : (char?)null;

            if (partialSource.Length == 2)
            {
                var sourceLocation = Location.Parse(partialSource);
                possibleSources = new[] { ctx[sourceLocation] };
            }
            else if (fileChar != null)
            {
                var sourceFile = FileHelpers.FromChar(fileChar.Value);
                possibleSources = possibleSources.Where(t => t.Location.File == sourceFile);
            }
            else if (rankChar != null)
            {
                var sourceRank = RankHelpers.FromChar(rankChar.Value);
                possibleSources = possibleSources.Where(t => t.Location.Rank == sourceRank);
            }

            try
            {
                var sourceTile = possibleSources.Single(t => t.Piece.Kind == sourceKind
                    // Castling should be denoted with the special notation O-O / O-O-O, we don't want to accept Kg1 / Kc1
                    && ctx.Inner.IsMovePseudoLegal(t.Location, destination, allowCastling: false));
                return sourceTile.Location;
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidMoveException(InvalidMoveReason.CouldNotInferSource, e);
            }
        }
    }
}
