using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Exceptions
{
    [Serializable]
    public class InvalidMoveException : Exception
    {
        public InvalidMoveException(string message) : base(message) { }
        public InvalidMoveException(string message, Exception inner) : base(message, inner) { }
    }
}
