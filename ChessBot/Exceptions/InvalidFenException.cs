using System;

namespace ChessBot.Exceptions
{
    [Serializable]
    public class InvalidFenException : Exception
    {
        public InvalidFenException() { }
    }
}
