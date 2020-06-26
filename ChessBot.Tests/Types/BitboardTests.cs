using ChessBot.Tests.TestHelpers;
using ChessBot.Types;
using System.Linq;
using Xunit;

namespace ChessBot.Tests.Types
{
    public class BitboardTests
    {
        [Theory]
        [InlineData(0, 0UL)]
        [InlineData(3, 0b100101UL)]
        [InlineData(64, ulong.MaxValue)]
        public void CountSetBits(int expected, ulong value)
        {
            Bitboard bb = value; // xUnit has problems with recognizing conversions to custom types
            Assert.Equal(expected, bb.CountSetBits());
        }

        [Theory]
        [InlineData(new[] { 0UL }, 0UL)]
        [InlineData(new[] { 0b000UL, 0b101UL, 0b100UL, 0b001UL }, 0b101UL)]
        public void PowerSet(ulong[] expected, ulong value)
        {
            Bitboard bb = value;
            Assert.Equal(expected.Select(v => (Bitboard)v), bb.PowerSet(), OrderInsensitiveComparer<Bitboard>.Instance);
        }
    }
}
