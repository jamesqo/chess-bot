using System;

namespace ChessBot.Exceptions
{
    [Serializable]
    public class AnParseException : Exception
    {
        public AnParseException(string message) : base(message) { }
        public AnParseException(string message, Exception inner) : base(message, inner) { }
    }
}
