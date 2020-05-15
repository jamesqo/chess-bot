using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using static ChessBot.ChessPiece;

namespace ChessBot.Tests
{
    public class ChessStateTests
    {
        [Fact(Skip = "not implemented yet")]
        public void ApplyMove_KingsideCastleWorks()
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
        public void ApplyMove_QueensideCastleWorks()
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
