using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ChessBot.Helpers
{
    internal static class SpanHelpers
    {
        public static ImmutableArray<T> ToImmutableArray<T>(this Span<T> span)
        {
            var array = span.ToArray();
            return Unsafe.As<T[], ImmutableArray<T>>(ref array);
        }
    }
}
