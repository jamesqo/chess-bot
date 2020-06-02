using System;

namespace ChessBot.Exceptions
{
    /// <summary>
    /// Exception thrown when an attempt to make an invalid chess move is made.
    /// </summary>
    [Serializable]
    public class InvalidMoveException : Exception
    {
        public InvalidMoveException(string message) : base(message) { }
        public InvalidMoveException(string message, Exception inner) : base(message, inner) { }
    }
}
