using System;
using static System.Console;

namespace ChessBot.Console
{
    interface ICommands
    {
        void ExitCommand();
        void HelpCommand();
        void MovesCommand(State state);
        void SearchTimesCommand(AI aiPlayer);
        State UndoCommand(State state);
    }

    class Commands : ICommands
    {
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

        public void MovesCommand(State state)
        {
            WriteLine("List of valid moves:");
            WriteLine();
            WriteLine(string.Join(Environment.NewLine, state.GetMoves()));
        }

        public void SearchTimesCommand(AI aiPlayer)
        {
            int moveCount = aiPlayer.History.Count;
            for (int i = 0; i < moveCount; i++)
            {
                var move = aiPlayer.History[i];
                var time = aiPlayer.SearchTimes[i];
                WriteLine($"{move} - {time.TotalMilliseconds}ms");
            }
        }

        public State UndoCommand(State state)
        {
            // todo: handle InvalidOperationException here
            var result = state.Undo().Undo(); // undo the last fullmove
            WriteLine(Helpers.GetDisplayString(result));
            WriteLine();
            WriteLine($"It's {result.ActiveSide}'s turn.");
            return result;
        }
    }
}
