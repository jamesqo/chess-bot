using ChessBot.Exceptions;
using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections.Generic;
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

        static AI GetAI()
        {
            IMovePicker inner;
            while (true)
            {
                Write("Pick ai strategy [alphabeta (default), mtdf, ids]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    case "": case "alphabeta": inner = new AlphaBeta(depth: 6); break;
                    case "mtdf": inner = new Mtdf(depth: 6); break;
                    case "ids": inner = new Ids(depth: 6); break;
                    default: continue;
                }
                return new AI(inner);
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
            var ai = GetAI();
            var fen = GetStartFen();
            WriteLine();

            WriteLine($"Playing as: {userSide}");
            WriteLine();

            var commands = new Commands();
            var state = new ProgramState
            {
                GameState = State.ParseFen(fen),
                HumanPlayer = new Human(commands),
                AIPlayer = ai
            };
            commands.State = state;
            bool justStarted = true;

            var whitePlayer = userSide.IsWhite() ? (IMovePicker)state.HumanPlayer : state.AIPlayer;
            var blackPlayer = userSide.IsWhite() ? (IMovePicker)state.AIPlayer : state.HumanPlayer;

            var gameState = state.GameState;
            while (true)
            {
                if (justStarted || gameState.WhiteToMove)
                {
                    WriteLine($"[Turn {gameState.FullMoveNumber}]");
                    WriteLine();
                }

                WriteLine(GetDisplayString(gameState));
                WriteLine();

                WriteLine($"It's {gameState.ActiveSide}'s turn.");
                var player = gameState.WhiteToMove ? whitePlayer : blackPlayer;
                var nextMove = player.PickMove(gameState);
                WriteLine($"{gameState.ActiveSide} played: {nextMove}");
                gameState = state.GameState = gameState.Apply(nextMove);
                WriteLine();
                CheckForEnd(gameState);

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

    class Human : IMovePicker
    {
        private readonly ICommands _handler;

        public Human(ICommands handler)
        {
            _handler = handler;
        }

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
                        _handler.ExitCommand();
                        break;
                    case "help":
                        _handler.HelpCommand();
                        break;
                    case "moves":
                        _handler.MovesCommand();
                        break;
                    case "searchtimes":
                        _handler.SearchTimesCommand();
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
                            WriteLine(e);
                            WriteLine("Sorry, try again.");
                        }
                        break;
                }
            }
        }
    }

    class AI : IMovePicker
    {
        private readonly IMovePicker _inner;
        private readonly List<Move> _history;
        private readonly List<TimeSpan> _searchTimes;
        private readonly Stopwatch _sw;

        public List<Move> History => _history;
        public List<TimeSpan> SearchTimes => _searchTimes;

        public AI(IMovePicker inner)
        {
            _inner = inner;
            _history = new List<Move>();
            _searchTimes = new List<TimeSpan>();
            _sw = new Stopwatch();
        }

        public Move PickMove(State root)
        {
            Debug.Assert(!_sw.IsRunning);
            Debug.Assert(_sw.Elapsed == TimeSpan.Zero);

            _sw.Start();
            var move = _inner.PickMove(root);
            _sw.Stop();

            _history.Add(move);
            _searchTimes.Add(_sw.Elapsed);
            _sw.Reset();

            return move;
        }
    }
}
