using ChessBot.Helpers;
using Xunit;

namespace ChessBot.Tests.Helpers
{
    public class PooledListTests
    {
        [Fact]
        public void Add_ExceedsCapacity_ResizesProperly()
        {
            using var list = PooledList<int>.Get(16);

            for (int i = 0; i < 100; i++) list.Add(i);

            Assert.Equal(100, list.Count);
            for (int i = 0; i < 100; i++) Assert.Equal(i, list[i]);
        }
    }
}
