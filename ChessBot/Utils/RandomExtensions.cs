using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Utils
{
    internal static class RandomExtensions
    {
        public static ulong NextULong(this Random rng)
        {
            return unchecked((ulong)(uint)rng.Next() | ((ulong)(uint)rng.Next() << 32));
        }
    }
}
