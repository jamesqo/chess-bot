using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    public class ChessMove
    {
        public static ChessMove Parse(string algebraicNotation, ChessState state)
        {
            throw new NotImplementedException();
        }

        public ChessMove(BoardLocation source, BoardLocation destination)
        {
            Source = source;
            Destination = destination;
        }

        public BoardLocation Source { get; }
        public BoardLocation Destination { get; }

        public override string ToString()
        {
            return $"{Source} > {Destination}";
        }
    }
}
