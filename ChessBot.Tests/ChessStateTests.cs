using ChessBot.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;
using static ChessBot.ChessPiece;

namespace ChessBot.Tests
{
    // todo: all of the assert.throws w/ AlgebraicNotationParseException should be moved to the ChessMove tests
    public class ChessStateTests
    {
        [Fact]
        public void ApplyMove_PawnAdvance()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/8/4P3/8 w - - 0 1");

            Assert.Equal(ChessState.ParseFen("8/8/8/8/8/4P3/8/8 b - - 1 1"), state.ApplyMove("e3"));
            Assert.Equal(ChessState.ParseFen("8/8/8/8/4P3/8/8/8 b - e3 1 1"), state.ApplyMove("e4"));

            state = state.ApplyMove("e3").SetActiveColor(PlayerColor.White);

            Assert.Equal(ChessState.ParseFen("8/8/8/8/4P3/8/8/8 b - - 2 1"), state.ApplyMove("e4"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e5"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByFriendlyPawn()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/4P3/4P3/8 w - - 0 1");

            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e3"));
            Assert.Equal(ChessState.ParseFen("8/8/8/8/4P3/8/4P3/8 b - - 1 1"), state.ApplyMove("e4"));

            state = state.ApplyMove("e4").SetActiveColor(PlayerColor.White);

            Assert.Equal(ChessState.ParseFen("8/8/8/8/4P3/4P3/8/8 b - - 2 1"), state.ApplyMove("e3"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByEnemyPawn()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/4p3/4P3/8 w - - 0 1");

            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e3"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));

            state = ChessState.ParseFen("8/8/8/8/4p3/8/4P3/8 w - - 0 1");

            Assert.Equal(ChessState.ParseFen("8/8/8/8/4p3/4P3/8/8 b - - 1 1"), state.ApplyMove("e3"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnCapture()
        {
            var state = ChessState.ParseFen("8/8/8/3p4/3PP3/8/8/8 w - - 0 1");

            // capture on left
            Assert.Equal(ChessState.ParseFen("8/8/8/3P4/3P4/8/8/8 b - - 1 1"), state.ApplyMove("xd5"));
            // todo
            // Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("d5"));

            state = ChessState.ParseFen("8/8/8/5p2/4PP2/8/8/8 w - - 0 1");

            // capture on right
            Assert.Equal(ChessState.ParseFen("8/8/8/5P2/5P2/8/8/8 b - - 1 1"), state.ApplyMove("xf5"));
            // todo
            // Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("f5"));
        }

        // todo: trying to capture when nothing is there

        [Fact]
        public void ApplyMove_KingsideCastle()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

            state = state.ApplyMove("O-O");
            Assert.Equal(ChessState.ParseFen("8/8/8/8/8/8/8/R4RK1 b kq - 1 1"), state);
        }

        [Fact]
        public void ApplyMove_QueensideCastle()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

            state = state.ApplyMove("O-O-O");
            Assert.Equal(ChessState.ParseFen("8/8/8/8/8/8/8/2KR3R b kq - 1 1"), state);
        }

        [Fact]
        public void ParseFen_Works()
        {
            var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            Assert.Equal(new ChessState(), ChessState.ParseFen(fen));

            // todo: add more tests
        }

        [Theory]
        [InlineData("i don't have 6 parts")]
        [InlineData("i do have 6 parts .")]
        // one of the fields is invalid
        [InlineData("foo w KQkq - 0 1")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR foo KQkq - 0 1")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w foo - 0 1", Skip = "not implemented yet")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq foo 0 1", Skip = "not implemented yet")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - foo 1", Skip = "not implemented yet")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 foo", Skip = "not implemented yet")]
        // bad number of ranks
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP w KQkq - 0 1")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR/RNBQKBNR w KQkq - 0 1")]
        // a rank isn't filled
        [InlineData("rnbqkbnr/ppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/7/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        // digits can't follow other digits
        [InlineData("rnbqkbnr/pppppppp/8/8/8/35/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        public void ParseFen_Fails(string badFen)
        {
            Assert.Throws<InvalidFenException>(() => ChessState.ParseFen(badFen));
        }
    }
}
