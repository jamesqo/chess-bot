using System;

namespace ChessBot.Exceptions
{
    /// <summary>
    /// Exception thrown when there's an error parsing algebraic notation.
    /// </summary>
    [Serializable]
    public class AnParseException : Exception
    {
        public AnParseException(string message) : base(message) { }
        public AnParseException(string message, Exception inner) : base(message, inner) { }
    }
}
