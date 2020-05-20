using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ChessBot.Tests
{
    internal static class Utils
    {
        public static void AssertEqual(ChessState expected, ChessState actual, bool ignoreActiveColor = true)
        {
            if (ignoreActiveColor && (expected.ActiveColor != actual.ActiveColor))
            {
                expected = expected.SetActiveColor(expected.OpposingColor);
            }

            Assert.Equal(expected, actual);
        }
    }
}
