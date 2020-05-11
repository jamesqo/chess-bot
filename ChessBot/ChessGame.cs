using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    public class ChessGame
    {
        private readonly List<ChessMove> _history;

        public ChessGame()
        {
            State = ChessState.Initial;
            Turn = 1;
            _history = new List<ChessMove>();
        }

        public ChessState State { get; private set; }
        public int Turn { get; private set; }
        public IEnumerable<ChessMove> History => _history;

        public void ApplyMove(ChessMove move)
        {
            State = State.ApplyMove(move);
            // todo: check for checkmate, stalemate, etc
            if (State.NextPlayer == PlayerColor.White) Turn++;
            _history.Add(move);
        }
    }
}
