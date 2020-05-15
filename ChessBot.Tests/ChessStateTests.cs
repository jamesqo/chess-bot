using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;
using static ChessBot.ChessPiece;

namespace ChessBot.Tests
{
    public class ChessStateTests
    {
        [Fact]
        public void ApplyMove_PawnAdvance()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
            });

            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e3"] = WhitePawn,
            }), state.ApplyMove("e3", togglePlayer: false));
            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e4", togglePlayer: false));

            state = state.ApplyMove("e3", togglePlayer: false);

            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e4", togglePlayer: false));
            Assert.Throws<InvalidChessMoveException>(() => state.ApplyMove("e5"));

            state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e3"] = WhitePawn,
            });

            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e3"));
            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e4", togglePlayer: false));

            state = state.ApplyMove("e4", togglePlayer: false);

            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e3"] = WhitePawn,
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e3", togglePlayer: false));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));

            state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e3"] = BlackPawn,
            });

            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e3"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));

            state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e4"] = BlackPawn,
            });

            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e3"] = WhitePawn,
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e3", togglePlayer: false));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnCapture()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e4"] = WhitePawn,
            });
        }

        [Fact(Skip = "not implemented yet")]
        public void ApplyMove_KingsideCastle()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["e1"] = WhiteKing,
                ["h1"] = WhiteRook,
            });

            state = state.ApplyMove("O-O");
            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["f1"] = WhiteRook,
                ["g1"] = WhiteKing,
            }), state);
        }

        [Fact(Skip = "not implemented yet")]
        public void ApplyMove_QueensideCastle()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["e1"] = WhiteKing,
                ["h1"] = WhiteRook,
            });

            state = state.ApplyMove("O-O");
            Assert.Equal(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["c1"] = WhiteKing,
                ["d1"] = WhiteRook,
                ["h1"] = WhiteRook,
            }), state);
        }
    }
}
