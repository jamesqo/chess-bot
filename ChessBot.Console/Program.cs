using ChessBot.Exceptions;
using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Console;

namespace ChessBot.Console
{
    class Program
    {
        static Side GetUserSide()
        {
            while (true)
            {
                Write("Pick your color [b, w (default)]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    case "b": return Side.Black;
                    case "": case "w": return Side.White;
                }
            }
        }

        static IMovePicker GetAIPicker()
        {
            while (true)
            {
                Write("Pick ai strategy [alphabeta (default), mtdf, ids]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    case "": case "alphabeta": return new AlphaBeta(depth: 5);
                    case "mtdf": return new Mtdf(depth: 5);
                    case "ids": return new Ids(depth: 5);
                }
            }
        }

        static string GetStartFen()
        {
            while (true)
            {
                Write("Enter start FEN [optional]: ");
                string input = ReadLine().Trim();
                if (string.IsNullOrEmpty(input)) return State.StartFen;

                try
                {
                    State.ParseFen(input); // make sure it's valid
                    return input;
                }
                catch (InvalidFenException) { }
            }
        }

        static void Main(string[] args)
        {
            WriteLine("Welcome! This is a simple chess bot written in C#.");
            WriteLine();

            var userSide = GetUserSide();
            var ai = GetAIPicker();
            var fen = GetStartFen();
            WriteLine();

            var whitePlayer = userSide.IsWhite() ? new HumanPicker() : ai;
            var blackPlayer = userSide.IsWhite() ? ai : new HumanPicker();

            WriteLine($"Playing as: {userSide}");
            WriteLine();

            var state = State.ParseFen(fen);
            bool justStarted = true;

            while (true)
            {
                if (justStarted || state.WhiteToMove)
                {
                    WriteLine($"[Turn {state.FullMoveNumber}]");
                    WriteLine();
                }

                WriteLine(GetDisplayString(state));
                WriteLine();

                WriteLine($"It's {state.ActiveSide}'s turn.");
                var player = state.WhiteToMove ? whitePlayer : blackPlayer;
                var nextMove = player.PickMove(state);
                WriteLine($"{state.ActiveSide} played: {nextMove}");
                state = state.Apply(nextMove);
                WriteLine();
                CheckForEnd(state);

                justStarted = false;
            }
        }

        static void CheckForEnd(State state)
        {
            bool isTerminal = !state.GetMoves().Any();
            bool isCheckmate = isTerminal && state.IsCheck;
            bool isStalemate = isTerminal && !state.IsCheck;

            if (isCheckmate)
            {
                WriteLine($"{state.OpposingSide} wins!");
                Environment.Exit(0);
            }

            if (isStalemate)
            {
                WriteLine("It's a draw.");
                Environment.Exit(0);
            }

            // todo: Check for 3-fold repetition, 50-move rule, etc.
        }

        static string GetDisplayString(State state)
        {
            var sb = new StringBuilder();
            // todo: have whichever side the human is on at the bottom
            for (var rank = Rank.Rank8; rank >= Rank.Rank1; rank--)
            {
                for (var file = File.FileA; file <= File.FileH; file++)
                {
                    if (file > File.FileA) sb.Append(' ');
                    sb.Append(GetDisplayChar(state[file, rank]));
                }
                if (rank > Rank.Rank1) sb.AppendLine();
            }
            return sb.ToString();
        }

        static char GetDisplayChar(Tile tile)
            => tile.HasPiece ? tile.Piece.ToDisplayChar() : '.';
    }

    class HumanPicker : IMovePicker
    {
        public Move PickMove(State root)
        {
            while (true)
            {
                Write("> ");
                string input = ReadLine().Trim();
                switch (input.ToLower())
                {
                    case "exit":
                    case "quit":
                        Environment.Exit(0);
                        break;
                    case "help":
                        // todo: have some kind of _commands object that you loop thru
                        WriteLine("List of commands:");
                        WriteLine();
                        WriteLine("exit|quit - exits the program");
                        WriteLine("help - displays this message");
                        WriteLine("list - lists all valid moves");
                        break;
                    case "list":
                        WriteLine("List of valid moves:");
                        WriteLine();
                        WriteLine(string.Join(Environment.NewLine, root.GetMoves()));
                        break;
                    default:
                        try
                        {
                            var move = Move.Parse(input, root);
                            _ = root.Apply(move); // make sure it's valid
                            return move;
                        }
                        catch (Exception e) when (e is AnParseException || e is InvalidMoveException)
                        {
                            Debug.WriteLine(e.ToString());
                            WriteLine("Sorry, try again.");
                        }
                        break;
                }
            }
        }
    }
}
