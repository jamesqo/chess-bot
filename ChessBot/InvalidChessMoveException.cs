using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    [Serializable]
    public class InvalidChessMoveException : Exception
    {
        public InvalidChessMoveException(string message) : base(message) { }
        public InvalidChessMoveException(string message, Exception inner) : base(message, inner) { }
    }
}
