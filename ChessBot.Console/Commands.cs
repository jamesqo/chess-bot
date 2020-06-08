using System;
using static System.Console;

namespace ChessBot.Console
{
    class Commands : ICommandHandler
    {
        public State Root { get; set; }
        public AI AIPlayer { get; set; }

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
            WriteLine("searchtime - lists times the ai took to execute each move");
        }

        public void MovesCommand()
        {
            WriteLine("List of valid moves:");
            WriteLine();
            WriteLine(string.Join(Environment.NewLine, Root.GetMoves()));
        }

        public void SearchTimesCommand()
        {
            for (int i = 0; i < AIPlayer.History.Count; i++)
            {
                var move = AIPlayer.History[i];
                var time = AIPlayer.SearchTimes[i];
                WriteLine($"{move} - {time.Milliseconds}ms");
            }
        }
    }
}
