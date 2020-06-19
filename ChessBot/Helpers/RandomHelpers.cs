using System;

namespace ChessBot.Helpers
{
    internal static class RandomHelpers
    {
        public static ulong NextUlong(this Random rng)
        {
            return unchecked((ulong)(uint)rng.Next() | ((ulong)(uint)rng.Next() << 32));
        }
    }
}
