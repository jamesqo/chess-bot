using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Exceptions
{
    public class AlgebraicNotationParseException : Exception
    {
        public AlgebraicNotationParseException() { }
        public AlgebraicNotationParseException(string message) : base(message) { }
        public AlgebraicNotationParseException(string message, Exception inner) : base(message, inner) { }
    }
}
