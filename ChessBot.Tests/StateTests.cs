﻿using ChessBot.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static ChessBot.Piece;

namespace ChessBot.Tests
{
    // todo: all of the assert.throws w/ AlgebraicNotationParseException should be moved to the ChessMove tests
    public class StateTests
    {
        [Fact]
        public void ApplyMove_PawnAdvance()
        {
            var state = State.ParseFen("8/8/8/8/8/8/4P3/8 w - - 0 1");

            Assert.Equal(State.ParseFen("8/8/8/8/8/4P3/8/8 b - - 1 1"), state.ApplyMove("e3"));
            Assert.Equal(State.ParseFen("8/8/8/8/4P3/8/8/8 b - e3 1 1"), state.ApplyMove("e4"));

            state = state.ApplyMove("e3").SetActiveColor(PlayerColor.White);

            Assert.Equal(State.ParseFen("8/8/8/8/4P3/8/8/8 b - - 2 1"), state.ApplyMove("e4"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("e5"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByFriendlyPawn()
        {
            var state = State.ParseFen("8/8/8/8/8/4P3/4P3/8 w - - 0 1");

            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("e3"));
            Assert.Equal(State.ParseFen("8/8/8/8/4P3/8/4P3/8 b - - 1 1"), state.ApplyMove("e4"));

            state = state.ApplyMove("e4").SetActiveColor(PlayerColor.White);

            Assert.Equal(State.ParseFen("8/8/8/8/4P3/4P3/8/8 b - - 2 1"), state.ApplyMove("e3"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByEnemyPawn()
        {
            var state = State.ParseFen("8/8/8/8/8/4p3/4P3/8 w - - 0 1");

            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("e3"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("e4"));

            state = State.ParseFen("8/8/8/8/4p3/8/4P3/8 w - - 0 1");

            Assert.Equal(State.ParseFen("8/8/8/8/4p3/4P3/8/8 b - - 1 1"), state.ApplyMove("e3"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnCapture()
        {
            var state = State.ParseFen("8/8/8/3p4/3PP3/8/8/8 w - - 0 1");

            // capture on left
            Assert.Equal(State.ParseFen("8/8/8/3P4/3P4/8/8/8 b - - 1 1"), state.ApplyMove("xd5"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("d5"));

            state = State.ParseFen("8/8/8/5p2/4PP2/8/8/8 w - - 0 1");

            // capture on right
            Assert.Equal(State.ParseFen("8/8/8/5P2/5P2/8/8/8 b - - 1 1"), state.ApplyMove("xf5"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("f5"));
        }

        // todo: trying to capture when nothing is there

        [Fact]
        public void ApplyMove_EnPassantCapture()
        {
            // black captures en passant
            var state = State.ParseFen("8/pppp1ppp/8/4P3/4p3/8/PPPP1PPP/8 w - - 0 1");

            // on left
            Assert.Equal(State.ParseFen("8/pppp1ppp/8/4P3/3Pp3/8/PPP2PPP/8 b - d3 1 1"), state.ApplyMove("d4"));
            Assert.Equal(State.ParseFen("8/pppp1ppp/8/4P3/8/3p4/PPP2PPP/8 w - - 1 2"), state.ApplyMove("d4").ApplyMove("xd3"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("d4").ApplyMove("xd5"));

            // on right
            Assert.Equal(State.ParseFen("8/pppp1ppp/8/4P3/4pP2/8/PPPP2PP/8 b - f3 1 1"), state.ApplyMove("f4"));
            Assert.Equal(State.ParseFen("8/pppp1ppp/8/4P3/8/5p2/PPPP2PP/8 w - - 1 2"), state.ApplyMove("f4").ApplyMove("xf3"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("d4").ApplyMove("xf5"));

            // white captures en passant
            state = state.SetActiveColor(PlayerColor.Black);

            // on left
            Assert.Equal(State.ParseFen("8/ppp2ppp/8/3pP3/4p3/8/PPPP1PPP/8 w - d6 1 1"), state.ApplyMove("d5"));
            Assert.Equal(State.ParseFen("8/ppp2ppp/3P4/8/4p3/8/PPPP1PPP/8 b - - 1 2"), state.ApplyMove("d5").ApplyMove("xd6"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("d5").ApplyMove("xd4"));

            // on right
            Assert.Equal(State.ParseFen("8/pppp2pp/8/4Pp2/4p3/8/PPPP1PPP/8 w - f6 1 1"), state.ApplyMove("f5"));
            Assert.Equal(State.ParseFen("8/pppp2pp/5P2/8/4p3/8/PPPP1PPP/8 b - - 1 2"), state.ApplyMove("f5").ApplyMove("xf6"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("f5").ApplyMove("xf4"));
        }

        [Fact]
        public void ApplyMove_KingsideCastle()
        {
            var state = State.ParseFen("8/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

            state = state.ApplyMove("O-O");
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/R4RK1 b kq - 1 1"), state);
        }

        [Fact]
        public void ApplyMove_QueensideCastle()
        {
            var state = State.ParseFen("8/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

            state = state.ApplyMove("O-O-O");
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/2KR3R b kq - 1 1"), state);
        }

        [Fact]
        public void ApplyMove_CastlingConditionsNotMet()
        {
            var state = State.ParseFen("8/8/8/8/8/8/8/R3K2R w KQ - 0 1");

            // king is moved
            var state2 = state.ApplyMove("Kd1").SetActiveColor(PlayerColor.White).ApplyMove("Ke1").SetActiveColor(PlayerColor.White);
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/R3K2R w - - 2 2"), state2);
            Assert.Throws<InvalidMoveException>(() => state2.ApplyMove("O-O"));
            Assert.Throws<InvalidMoveException>(() => state2.ApplyMove("O-O-O"));

            // rook is moved
            state2 = state.ApplyMove("Ra2").SetActiveColor(PlayerColor.White).ApplyMove("Ra1").SetActiveColor(PlayerColor.White);
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/R3K2R w K - 2 2"), state2);
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/R4RK1 b - - 3 2"), state2.ApplyMove("O-O"));
            Assert.Throws<InvalidMoveException>(() => state2.ApplyMove("O-O-O"));

            state2 = state.ApplyMove("Rh2").SetActiveColor(PlayerColor.White).ApplyMove("Rh1").SetActiveColor(PlayerColor.White);
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/R3K2R w Q - 2 2"), state2);
            Assert.Throws<InvalidMoveException>(() => state2.ApplyMove("O-O"));
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/8/2KR3R b - - 3 2"), state2.ApplyMove("O-O-O"));

            // rook is captured
            state = State.ParseFen("8/8/8/8/8/8/r6r/R3K2R b KQ - 0 1");
            state2 = state.ApplyMove("Rxa1");
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/7r/r3K2R w K - 1 1"), state2);
            Assert.Throws<InvalidMoveException>(() => state2.ApplyMove("O-O-O"));
            state2 = state.ApplyMove("Rxh1");
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/r7/R3K2r w Q - 1 1"), state2);
            Assert.Throws<InvalidMoveException>(() => state2.ApplyMove("O-O"));

            // friendly piece in between king and rook
            state = State.ParseFen("8/8/8/8/8/8/8/R2BK1BR w KQ - 0 1");
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O-O"));

            // enemy piece in between king and rook
            state = State.ParseFen("8/8/8/8/8/8/8/R2bK1bR w KQ - 0 1");
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O-O"));

            // king is in check
            state = State.ParseFen("8/8/8/8/8/8/4r3/R3K2R w KQ - 0 1");
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O-O"));

            // king passes through attacked location (queenside)
            state = State.ParseFen("8/8/8/8/8/8/3r4/R3K2R w KQ - 0 1");
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/3r4/R4RK1 b - - 1 1"), state.ApplyMove("O-O"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O-O"));

            // king passes through attacked location (kingside)
            state = State.ParseFen("8/8/8/8/8/8/5r2/R3K2R w KQ - 0 1");
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O"));
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/3r4/2KR3R b - - 1 1"), state.ApplyMove("O-O-O"));

            // b1 square is occupied
            state = State.ParseFen("8/8/8/8/8/8/8/RN2K2R w Q - 0 1");
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("O-O-O"));

            // b1 square is attacked (should not prevent queenside castling)
            state = State.ParseFen("8/8/8/8/8/8/1r6/R3K2R w Q - 0 1");
            Assert.Equal(State.ParseFen("8/8/8/8/8/8/1r6/2KR3R b - - 1 1"), state.ApplyMove("O-O-O"));

            // todo: king castles into check
        }

        [Fact]
        public void ApplyMove_Promotion()
        {
            var state = State.ParseFen("8/P7/8/8/8/8/p7/8 w - - 0 1");

            Assert.Equal(State.ParseFen("N7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8N"));
            Assert.Equal(State.ParseFen("B7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8B"));
            Assert.Equal(State.ParseFen("R7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8R"));
            Assert.Equal(State.ParseFen("Q7/8/8/8/8/8/p7/8 b - - 1 1"), state.ApplyMove("a8Q"));

            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("a8"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("a8P"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("a8K"));

            state = state.SetActiveColor(PlayerColor.Black);

            Assert.Equal(State.ParseFen("8/P7/8/8/8/8/8/n7 w - - 1 1"), state.ApplyMove("a1N"));
            Assert.Equal(State.ParseFen("8/P7/8/8/8/8/8/b7 w - - 1 1"), state.ApplyMove("a1B"));
            Assert.Equal(State.ParseFen("8/P7/8/8/8/8/8/r7 w - - 1 1"), state.ApplyMove("a1R"));
            Assert.Equal(State.ParseFen("8/P7/8/8/8/8/8/q7 w - - 1 1"), state.ApplyMove("a1Q"));

            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("a1"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("a1P"));
            Assert.Throws<InvalidMoveException>(() => state.ApplyMove("a1K"));
        }

        [Fact(Skip = "test needs fixing")]
        public void GetMoves_GetSuccessors()
        {
            var state = State.Start;

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
            }.Select(an => Move.Parse(an, state));
            var succs = moves.Select(m => state.ApplyMove(m));
            var movesAndSuccs = moves.Zip(succs);

            Assert.Equal(moves, state.GetMoves());
            Assert.Equal(succs, state.GetSuccessors());
            Assert.Equal(movesAndSuccs, state.GetMovesAndSuccessors());
        }
        
        // todo: add tests for castling for GetMoves/Successors

        [Fact(Skip = "test needs fixing")]
        public void GetMoves_GetSuccessors_EnPassantCapture()
        {
            // black captures en passant
            var state = State.ParseFen("8/8/8/8/4p3/8/3P1P2/8 w - - 0 1");

            // on left
            var state2 = state.ApplyMove("d4");
            var moves = new[] { "e3", "xd3" }.Select(an => Move.Parse(an, state2));
            Assert.Equal(moves, state2.GetMoves());
            // on right
            state2 = state.ApplyMove("f4");
            moves = new[] { "e3", "xf3" }.Select(an => Move.Parse(an, state2));
            Assert.Equal(moves, state2.GetMoves());

            // white captures en passant
            state = State.ParseFen("8/3p1p2/8/4P3/8/8/8/8 b - - 0 1");

            // on left
            state2 = state.ApplyMove("d5");
            moves = new[] { "e6", "xd6" }.Select(an => Move.Parse(an, state2));
            Assert.Equal(moves, state2.GetMoves());
            // on right
            state2 = state.ApplyMove("f5");
            moves = new[] { "e6", "xf6" }.Select(an => Move.Parse(an, state2));
            Assert.Equal(moves, state2.GetMoves());
        }

        [Fact(Skip = "test needs fixing")]
        public void GetMoves_GetSuccessors_Promotion()
        {
            var state = State.ParseFen("8/P7/8/8/8/8/p7/8 w - - 0 1");

            var moves = new[] { "a8N", "a8B", "a8R", "a8Q" }.Select(an => Move.Parse(an, state));
            var succs = moves.Select(m => state.ApplyMove(m));
            var movesAndSuccs = moves.Zip(succs);

            Assert.Equal(moves, state.GetMoves());
            Assert.Equal(succs, state.GetSuccessors());
            Assert.Equal(movesAndSuccs, state.GetMovesAndSuccessors());

            state = state.SetActiveColor(PlayerColor.Black);

            moves = new[] { "a1N", "a1B", "a1R", "a1Q" }.Select(an => Move.Parse(an, state));
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
            Assert.Equal(State.Start, State.ParseFen(fen)); // todo: this isn't actually testing anything

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
            Assert.Throws<InvalidFenException>(() => State.ParseFen(badFen));
        }
    }
}