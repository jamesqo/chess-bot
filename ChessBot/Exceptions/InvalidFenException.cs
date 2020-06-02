using System;

namespace ChessBot.Exceptions
{
    /// <summary>
    /// Exception thrown when trying to parse an invalid FEN string.
    /// </summary>
    [Serializable]
    public class InvalidFenException : Exception
    {
        public InvalidFenException(string message) : base(message) { }
        public InvalidFenException(string message, Exception inner) : base(message, inner) { }
    }
}
