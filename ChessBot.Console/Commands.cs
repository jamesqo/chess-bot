using System;
using static System.Console;

namespace ChessBot.Console
{
    interface ICommands
    {
        void ExitCommand();
        void HelpCommand();
        void MovesCommand();
        void SearchTimesCommand();
    }

    class Commands : ICommands
    {
        public IProgramState State { get; set; }

        public void ExitCommand()
        {
            Environment.Exit(0);
        }

        public void HelpCommand()
        {
            WriteLine("List of commands:");
            WriteLine();
            WriteLine("exit|quit - exits the program");
            WriteLine("help - displays this message");
            WriteLine("moves - lists all valid moves");
            WriteLine("searchtimes - lists times the ai took to execute each move");
        }

        public void MovesCommand()
        {
            WriteLine("List of valid moves:");
            WriteLine();
            WriteLine(string.Join(Environment.NewLine, _state.GameState.GetMoves()));
        }

        public void SearchTimesCommand()
        {
            int moveCount = _state.AIPlayer.History.Count;
            for (int i = 0; i < moveCount; i++)
            {
                var move = _state.AIPlayer.History[i];
                var time = _state.AIPlayer.SearchTimes[i];
                WriteLine($"{move} - {time.TotalMilliseconds}ms");
            }
        }
    }
}
