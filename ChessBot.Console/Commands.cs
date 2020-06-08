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
        void UndoCommand();
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
            WriteLine("undo - undoes the last fullmove");
        }

        public void MovesCommand()
        {
            WriteLine("List of valid moves:");
            WriteLine();
            WriteLine(string.Join(Environment.NewLine, State.GameState.GetMoves()));
        }

        public void SearchTimesCommand()
        {
            int moveCount = State.AIPlayer.History.Count;
            for (int i = 0; i < moveCount; i++)
            {
                var move = State.AIPlayer.History[i];
                var time = State.AIPlayer.SearchTimes[i];
                WriteLine($"{move} - {time.TotalMilliseconds}ms");
            }
        }

        public void UndoCommand()
        {
            // todo: handle InvalidOperationException
            State.GameState = State.GameState.Undo().Undo(); // undo the last fullmove
            WriteLine(Helpers.GetDisplayString(State.GameState));
            WriteLine();
            WriteLine($"It's {State.GameState.ActiveSide}'s turn.");
        }
    }
}
