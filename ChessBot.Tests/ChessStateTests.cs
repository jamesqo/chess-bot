using System.Collections.Generic;
using Xunit;
using static ChessBot.ChessPiece;
using static ChessBot.Tests.Utils;

namespace ChessBot.Tests
{
    // todo: all of the assert.throws w/ AlgebraicNotationParseException should be moved to the ChessMove tests
    public class ChessStateTests
    {
        [Fact]
        public void ApplyMove_PawnAdvance()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
            });

            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e3"] = WhitePawn,
            }), state.ApplyMove("e3"));
            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e4"));

            state = state.ApplyMove("e3").SetActiveColor(PlayerColor.White);

            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e4"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e5"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByFriendlyPawn()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e3"] = WhitePawn,
            });

            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e3"));
            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e2"] = WhitePawn,
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e4"));

            state = state.ApplyMove("e4").SetActiveColor(PlayerColor.White);

            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e3"] = WhitePawn,
                ["e4"] = WhitePawn,
            }), state.ApplyMove("e3"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnAdvance_BlockedByEnemyPawn()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
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

            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e3"] = WhitePawn,
                ["e4"] = BlackPawn,
            }), state.ApplyMove("e3"));
            Assert.Throws<AlgebraicNotationParseException>(() => state.ApplyMove("e4"));
        }

        [Fact]
        public void ApplyMove_PawnCapture()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["d4"] = WhitePawn,
                ["e4"] = WhitePawn,
                ["d5"] = BlackPawn,
            });

            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["d4"] = WhitePawn,
                ["d5"] = WhitePawn,
            }), state.ApplyMove("xd5"));

            state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["e4"] = WhitePawn,
                ["f4"] = WhitePawn,
                ["f5"] = BlackPawn,
            });

            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["f4"] = WhitePawn,
                ["f5"] = WhitePawn,
            }), state.ApplyMove("xf5"));
        }

        // todo: trying to capture when nothing is there

        [Fact]
        public void ApplyMove_KingsideCastle()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["e1"] = WhiteKing,
                ["h1"] = WhiteRook,
            });

            state = state.ApplyMove("O-O");
            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["f1"] = WhiteRook,
                ["g1"] = WhiteKing,
            },
            white: new PlayerInfo(PlayerColor.White, hasCastled: true, hasMovedKing: true, hasMovedKingsideRook: true)), state);
        }

        [Fact]
        public void ApplyMove_QueensideCastle()
        {
            var state = new ChessState(new Dictionary<string, ChessPiece>
            {
                ["a1"] = WhiteRook,
                ["e1"] = WhiteKing,
                ["h1"] = WhiteRook,
            });

            state = state.ApplyMove("O-O-O");
            AssertEqual(new ChessState(new Dictionary<string, ChessPiece>
            {
                ["c1"] = WhiteKing,
                ["d1"] = WhiteRook,
                ["h1"] = WhiteRook,
            },
            white: new PlayerInfo(PlayerColor.White, hasCastled: true, hasMovedKing: true, hasMovedQueensideRook: true)), state);
        }
    }
}
