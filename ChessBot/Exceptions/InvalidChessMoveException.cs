using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Exceptions
{
    [Serializable]
    public class InvalidChessMoveException : Exception
    {
        public InvalidChessMoveException(string message) : base(message) { }
        public InvalidChessMoveException(string message, Exception inner) : base(message, inner) { }
    }
}
