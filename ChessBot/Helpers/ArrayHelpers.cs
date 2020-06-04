using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ChessBot.Helpers
{
    internal static class ArrayHelpers
    {
        public static ImmutableArray<T> UnsafeAsImmutable<T>(this T[] array)
        {
            return Unsafe.As<T[], ImmutableArray<T>>(ref array);
        }
    }
}
