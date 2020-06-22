using ChessBot.Helpers;
using ChessBot.Tests.TestHelpers;
using ChessBot.Types;
using System.Linq;
using Xunit;

namespace ChessBot.Tests.Helpers
{
    public class MagicTests
    {
        [Theory]
        [InlineData(new[] { "d3", "c2", "f5", "g6", "d5", "c6", "b7", "a8", "f3", "g2", "h1" }, new[] { "c2", "g6" }, "e4")]
        public void BishopAttacks(string[] expected, string[] occupied, string source)
        {
            var sourceLoc = Location.Parse(source);
            var occupiedBb = Bitboard.FromLocations(occupied.Select(Location.Parse));

            var unblockedAttackBb = StaticInfo.GetAttackBitboard(Piece.WhiteBishop, sourceLoc);
            var actual = Magic.BishopAttacks(unblockedAttackBb, occupiedBb, sourceLoc);

            Assert.Equal(expected.Select(Location.Parse), actual.Locations(), OrderInsensitiveComparer<Location>.Instance);
        }
    }
}
