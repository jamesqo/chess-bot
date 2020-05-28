using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Exceptions
{
    public class AnParseException : Exception
    {
        public AnParseException() { }
        public AnParseException(string message) : base(message) { }
        public AnParseException(string message, Exception inner) : base(message, inner) { }
    }
}
