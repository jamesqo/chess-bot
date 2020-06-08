namespace ChessBot.Console
{
    interface IProgramState
    {
        Human HumanPlayer { get; }
        AI AIPlayer { get; }
        State GameState { get; }
    }

    class ProgramState : IProgramState
    {
        public Human HumanPlayer { get; set; }

        public AI AIPlayer { get; set; }

        public State GameState { get; set; }
    }
}
