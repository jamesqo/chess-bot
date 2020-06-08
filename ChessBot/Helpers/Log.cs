using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Helpers
{
    internal static class Log
    {
        public static void Debug(string message, params object[] formatArgs)
        {
            System.Diagnostics.Debug.WriteLine(message, formatArgs);
        }
    }
}
