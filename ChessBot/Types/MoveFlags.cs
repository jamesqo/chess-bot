using System;

namespace ChessBot.Types
{
    [Flags]
    public enum MoveFlags
    {
        Captures = 1,
        NonCaptures = Captures << 1,

        Default = Captures | NonCaptures,
    }
}
