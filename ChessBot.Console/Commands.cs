using System;
using static System.Console;

namespace ChessBot.Console
{
    interface ICommands
    {
        void Exit();
        void Help();
        void Moves(State state);
        void SearchInfo(AI aiPlayer);
        State Undo(State state);
    }

    class Commands : ICommands
    {
        public void Exit()
        {
            Environment.Exit(0);
        }

        public void Help()
        {
            WriteLine("List of commands:");
            WriteLine();
            WriteLine("exit|quit - exits the program");
            WriteLine("help - displays this message");
            WriteLine("moves - lists all valid moves");
            WriteLine("searchinfo - lists search information for each move made by the ai");
            WriteLine("undo - undoes the last fullmove");
        }

        public void Moves(State state)
        {
            WriteLine("List of valid moves:");
            WriteLine();
            WriteLine(string.Join(Environment.NewLine, state.GetMoves()));
        }

        public void SearchInfo(AI aiPlayer)
        {
            int moveCount = aiPlayer.History.Count;
            for (int i = 0; i < moveCount; i++)
            {
                var move = aiPlayer.History[i];
                var info = aiPlayer.SearchInfos[i];
                WriteLine($"{i + 1}. {move} - score {info.Score} / pv {string.Join(' ', info.Pv)} / elapsed {(int)info.Elapsed.TotalMilliseconds}ms");
            }
        }

        public State Undo(State state)
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
