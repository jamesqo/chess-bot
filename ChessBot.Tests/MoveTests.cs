using System;
using System.Collections.Generic;
using Xunit;

namespace ChessBot.Tests
{
    public class MoveTests
    {
        // todo: some tests ie. en passant captures need to be moved here

        [Theory]
        [InlineData("O-O")]
        [InlineData("0-0")]
        public void Parse_KingsideCastle(string moveString)
        {
            var state = State.ParseFen("8/8/8/8/8/8/8/R3K2R w KQ - 0 1");

            // todo: should we throw here if the castling flags are not set, or wait until ApplyMove?
            var move = Move.Parse(moveString, state);
            Assert.True(move.IsKingsideCastle);
            Assert.False(move.IsQueensideCastle);
            Assert.False(move.IsCapture);
            Assert.Equal(Location.Parse("e1"), move.Source);
            Assert.Equal(Location.Parse("g1"), move.Destination);
        }

        [Theory]
        [InlineData("O-O-O")]
        [InlineData("0-0-0")]
        public void Parse_QueensideCastle(string moveString)
        {
            var state = State.ParseFen("8/8/8/8/8/8/8/R3K2R w KQ - 0 1");

            var move = Move.Parse(moveString, state);
            Assert.True(move.IsQueensideCastle);
            Assert.False(move.IsKingsideCastle);
            Assert.False(move.IsCapture);
            Assert.Equal(Location.Parse("e1"), move.Source);
            Assert.Equal(Location.Parse("c1"), move.Destination);
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityFromPawnCapture()
        {
            var state = State.ParseFen("8/8/8/4p3/3PP3/8/8/8 w - - 0 1");

            var move = Move.Parse("xe5", state);
            Assert.Equal(Location.Parse("d4"), move.Source);

            // todo: what should we do for "e5" here
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityIfPawnIsBlocked()
        {
            var state = State.ParseFen("8/8/8/8/8/4P3/4P3/8 w - - 0 1");

            var move = Move.Parse("e4", state);
            Assert.Equal(Location.Parse("e3"), move.Source);
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityIfBishopIsBlocked()
        {
            var state = State.ParseFen("8/8/8/2B1B3/3B4/2B1B3/8/8 w - - 0 1");

            var move = Move.Parse("Bf6", state);
            Assert.Equal(Location.Parse("e5"), move.Source);

            move = Move.Parse("Bf2", state);
            Assert.Equal(Location.Parse("e3"), move.Source);

            move = Move.Parse("Bb2", state);
            Assert.Equal(Location.Parse("c3"), move.Source);

            move = Move.Parse("Bb6", state);
            Assert.Equal(Location.Parse("c5"), move.Source);
        }

        [Fact]
        public void Parse_ShouldResolveAmbiguityIfRookIsBlocked()
        {
            var state = State.ParseFen("8/8/8/3R4/2RRR3/3R4/8/8 w - - 0 1");

            var move = Move.Parse("Rf4", state);
            Assert.Equal(Location.Parse("e4"), move.Source);

            move = Move.Parse("Rb4", state);
            Assert.Equal(Location.Parse("c4"), move.Source);

            move = Move.Parse("Rd6", state);
            Assert.Equal(Location.Parse("d5"), move.Source);

            move = Move.Parse("Rd2", state);
            Assert.Equal(Location.Parse("d3"), move.Source);
        }

        // todo: add ambiguity tests for queen
    }
}
