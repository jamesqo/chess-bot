using ChessBot.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e5"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByFriendlyPawn()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/4P3/4P3/8 w - - 0 1");

            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e3"));
            Assert.Equal(ChessState.ParseFen("8/8/8/8/4P3/8/4P3/8 b - - 1 1"), state.ApplyMove("e4"));

            state = state.ApplyMove("e4").SetActiveColor(PlayerColor.White);

            Assert.Equal(ChessState.ParseFen("8/8/8/8/4P3/4P3/8/8 b - - 2 1"), state.ApplyMove("e3"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByEnemyPawn()
        {
            var state = ChessState.ParseFen("8/8/8/8/8/4p3/4P3/8 w - - 0 1");

            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e3"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e4"));

            state = ChessState.ParseFen("8/8/8/8/4p3/8/4P3/8 w - - 0 1");

            Assert.Equal(ChessState.ParseFen("8/8/8/8/4p3/4P3/8/8 b - - 1 1"), state.ApplyMove("e3"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnCapture()
        {
            var state = ChessState.ParseFen("8/8/8/3p4/3PP3/8/8/8 w - - 0 1");

            // capture on left
            Assert.Equal(ChessState.ParseFen("8/8/8/3P4/3P4/8/8/8 b - - 1 1"), state.ApplyMove("xd5"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("d5"));

            state = ChessState.ParseFen("8/8/8/5p2/4PP2/8/8/8 w - - 0 1");

            // capture on right
            Assert.Equal(ChessState.ParseFen("8/8/8/5P2/5P2/8/8/8 b - - 1 1"), state.ApplyMove("xf5"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("f5"));
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
        public void ApplyMove_Promotion()
        {
            var state = ChessState.ParseFen("8/P7/8/8/8/8/p7/8 w - - 0 1");

            Assert.Equal(ChessState.ParseFen("N7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8N"));
            Assert.Equal(ChessState.ParseFen("B7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8B"));
            Assert.Equal(ChessState.ParseFen("R7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8R"));
            Assert.Equal(ChessState.ParseFen("Q7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8Q"));

            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("a8"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("a8P"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("a8K"));

            state = state.SetActiveColor(PlayerColor.Black);

            Assert.Equal(ChessState.ParseFen("8/P7/8/8/8/8/8/n7 w - - 1 1"), state.ApplyMove("a1N"));
            Assert.Equal(ChessState.ParseFen("8/P7/8/8/8/8/8/b7 w - - 1 1"), state.ApplyMove("a1B"));
            Assert.Equal(ChessState.ParseFen("8/P7/8/8/8/8/8/r7 w - - 1 1"), state.ApplyMove("a1R"));
            Assert.Equal(ChessState.ParseFen("8/P7/8/8/8/8/8/q7 w - - 1 1"), state.ApplyMove("a1Q"));

            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("a1"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("a1P"));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("a1K"));
        }

        [Fact]
        public void GetMoves_GetSuccessors_Work()
        {
            var state = ChessState.Start;

            var moves = new[]
            {
                "a3",
                "a4",
                "b3",
                "b4",
                "c3",
                "c4",
                "d3",
                "d4",
                "e3",
                "e4",
                "f3",
                "f4",
                "g3",
                "g4",
                "h3",
                "h4",
                "Nf1",
                "Nf3",
                "Nf6",
                "Nf8",
            }.Select(an => ChessMove.Parse(an, state));
            var succs = moves.Select(m => state.ApplyMove(m));
            var movesAndSuccs = moves.Zip(succs);

            Assert.Equal(moves, state.GetMoves());
            Assert.Equal(succs, state.GetSuccessors());
            Assert.Equal(movesAndSuccs, state.GetMovesAndSuccessors());
        }

        [Fact]
        public void GetMoves_GetSuccessors_Promotion()
        {
            var state = ChessState.ParseFen("8/P7/8/8/8/8/8/p7/8 w - - 0 1");

            var moves = new[] { "a8N", "a8B", "a8R", "a8Q" }.Select(an => ChessMove.Parse(an, state));
            var succs = moves.Select(m => state.ApplyMove(m));
            var movesAndSuccs = moves.Zip(succs);

            Assert.Equal(moves, state.GetMoves());
            Assert.Equal(succs, state.GetSuccessors());
            Assert.Equal(movesAndSuccs, state.GetMovesAndSuccessors());

            state = state.SetActiveColor(PlayerColor.Black);

            moves = new[] { "a1N", "a1B", "a1R", "a1Q" }.Select(an => ChessMove.Parse(an, state));
            succs = moves.Select(m => state.ApplyMove(m));
            movesAndSuccs = moves.Zip(succs);

            Assert.Equal(moves, state.GetMoves());
            Assert.Equal(succs, state.GetSuccessors());
            Assert.Equal(movesAndSuccs, state.GetMovesAndSuccessors());
        }

        [Fact]
        public void ParseFen_Works()
        {
            var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            Assert.Equal(ChessState.Start, ChessState.ParseFen(fen)); // todo: this isn't actually testing anything

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
