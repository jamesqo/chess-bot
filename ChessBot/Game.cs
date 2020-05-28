using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    public class Game
    {
        private readonly List<Move> _history;

        public Game()
        {
            State = State.Start;
            Turn = 1;
            _history = new List<Move>();
        }

        public State State { get; private set; }
        public int Turn { get; private set; }
        public IEnumerable<Move> History => _history;

        public void ApplyMove(Move move)
        {
            State = State.ApplyMove(move);
            // todo: check for checkmate, stalemate, etc
            if (State.WhiteToMove) Turn++;
            _history.Add(move);
        }
    }
}
