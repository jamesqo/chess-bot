using ChessBot.Tests.TestHelpers;
using ChessBot.Types;
using System.Linq;
using Xunit;

namespace ChessBot.Tests
{
    public class StaticInfoTests
    {
        [Theory]
        [InlineData(new[] { "d3", "c2", "f5", "g6", "h7", "d5", "c6", "b7", "a8", "f3", "g2", "h1" }, new[] { "c2" }, "e4")]
        [InlineData(new[] { "d3", "c2", "f5", "g6", "d5", "c6", "b7", "a8", "f3", "g2", "h1" }, new[] { "c2", "g6" }, "e4")]
        public void GetAttackBitboard_Bishop(string[] expected, string[] occupied, string source)
        {
            var sourceLoc = Location.Parse(source);
            var occupiedBb = Bitboard.FromLocations(occupied.Select(Location.Parse));

            var actual = StaticInfo.GetAttackBitboard(Piece.WhiteBishop, sourceLoc, occupiedBb);

            Assert.Equal(expected.Select(Location.Parse), actual.Locations(), OrderInsensitiveComparer<Location>.Instance);
        }

        [Theory]
        [InlineData(new[] { "a8", "b8", "c8", "d8", "e8", "f8", "g8", "h7", "h6" }, new[] { "a8", "h6" }, "h8")]
        public void GetAttackBitboard_Rook(string[] expected, string[] occupied, string source)
        {
            var sourceLoc = Location.Parse(source);
            var occupiedBb = Bitboard.FromLocations(occupied.Select(Location.Parse));

            var actual = StaticInfo.GetAttackBitboard(Piece.WhiteRook, sourceLoc, occupiedBb);

            Assert.Equal(expected.Select(Location.Parse), actual.Locations(), OrderInsensitiveComparer<Location>.Instance);
        }

        [Theory]
        [InlineData(new[] { "a3", "b3", "c3", "c2", "d2", "c1", "b1", "a1", "a2" }, new[] { "a4", "b3", "c3", "d2" }, "b2")]
        public void GetAttackBitboard_Queen(string[] expected, string[] occupied, string source)
        {
            var sourceLoc = Location.Parse(source);
            var occupiedBb = Bitboard.FromLocations(occupied.Select(Location.Parse));

            var actual = StaticInfo.GetAttackBitboard(Piece.WhiteQueen, sourceLoc, occupiedBb);

            Assert.Equal(expected.Select(Location.Parse), actual.Locations(), OrderInsensitiveComparer<Location>.Instance);
        }
    }
}
