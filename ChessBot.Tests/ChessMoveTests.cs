using System;
using System.Collections.Generic;
using Xunit;
using static ChessBot.ChessPiece;

namespace ChessBot.Tests
{
    public class ChessMoveTests
    {
        [Theory]
        [InlineData("O-O")]
        [InlineData("0-0")]
        public void Parse_KingsideCastle(string moveString)
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["e1"] = WhiteKing,
                ["h1"] = WhiteRook,
            });

            var move = ChessMove.Parse(moveString, state);
            Assert.True(move.IsKingsideCastle);
            Assert.False(move.IsQueensideCastle);
            Assert.False(move.IsCapture);
            Assert.Equal(BoardLocation.Parse("e1"), move.Source);
            Assert.Equal(BoardLocation.Parse("g1"), move.Destination);
        }

        [Theory]
        [InlineData("O-O-O")]
        [InlineData("0-0-0")]
        public void Parse_QueensideCastle(string moveString)
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["e1"] = WhiteKing,
                ["h1"] = WhiteRook,
            });

            var move = ChessMove.Parse(moveString, state);
            Assert.True(move.IsQueensideCastle);
            Assert.False(move.IsKingsideCastle);
            Assert.False(move.IsCapture);
            Assert.Equal(BoardLocation.Parse("e1"), move.Source);
            Assert.Equal(BoardLocation.Parse("c1"), move.Destination);
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityFromPawnCapture()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["d4"] = WhitePawn,
                ["e4"] = WhitePawn,
                ["e5"] = BlackPawn,
            });

            var move = ChessMove.Parse("xe5", state);
            Assert.Equal(BoardLocation.Parse("d4"), move.Source);

            // todo: what should we do for "e5" here
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityIfPawnIsBlocked()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e3"] = WhitePawn,
            });

            var move = ChessMove.Parse("e4", state);
            Assert.Equal(BoardLocation.Parse("e3"), move.Source);
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityIfBishopIsBlocked()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["d4"] = WhiteBishop,
                ["e5"] = WhiteBishop,
                ["e3"] = WhiteBishop,
                ["c5"] = WhiteBishop,
                ["c3"] = WhiteBishop,
            });

            var move = ChessMove.Parse("Bf6", state);
            Assert.Equal(BoardLocation.Parse("e5"), move.Source);

            move = ChessMove.Parse("Bf2", state);
            Assert.Equal(BoardLocation.Parse("e3"), move.Source);

            move = ChessMove.Parse("Bb2", state);
            Assert.Equal(BoardLocation.Parse("c3"), move.Source);

            move = ChessMove.Parse("Bb6", state);
            Assert.Equal(BoardLocation.Parse("c5"), move.Source);
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityIfRookIsBlocked()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["d4"] = WhiteRook,
                ["e4"] = WhiteRook,
                ["c4"] = WhiteRook,
                ["d5"] = WhiteRook,
                ["d3"] = WhiteRook,
            });

            var move = ChessMove.Parse("Rf4", state);
            Assert.Equal(BoardLocation.Parse("e4"), move.Source);

            move = ChessMove.Parse("Rb4", state);
            Assert.Equal(BoardLocation.Parse("c4"), move.Source);

            move = ChessMove.Parse("Rd6", state);
            Assert.Equal(BoardLocation.Parse("d5"), move.Source);

            move = ChessMove.Parse("Rd2", state);
            Assert.Equal(BoardLocation.Parse("d3"), move.Source);
        }

        // todo: add ambiguity tests for queen
    }
}
