using System;

namespace ChessBot.Exceptions
{
    [Serializable]
    public class InvalidFenException : Exception
    {
        public InvalidFenException() { }
        public InvalidFenException(string message) : base(message) { }
        public InvalidFenException(string message, Exception inner) : base(message, inner) { }
    }
}
