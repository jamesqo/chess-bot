using System.Runtime.CompilerServices;

namespace ChessBot.Utils
{
    internal static class IntHelpers
    {
        // This doesn't check for int.MinValue like Math.Abs() does, so it performs slightly better
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Abs(this int value)
        {
            return unchecked((value >= 0) ? value : -value);
        }
    }
}
