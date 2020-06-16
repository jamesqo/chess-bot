using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.Console;

namespace ChessBot.Console
{
    class Program
    {
        static readonly ICommands Commands = new Commands();

        static Side GetHumanSide()
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
            ISearchAlgorithm inner;
            while (true)
            {
                Write("Pick ai strategy [mtdf (default), ids]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    //case "alphabeta": inner = new AlphaBeta(depth: 6); break;
                    case "":  case "mtdf": inner = new Mtdf(depth: 7, ttCapacity: (1 << 16)); break;
                    case "ids": inner = new Ids(depth: 7, ttCapacity: (1 << 16)); break;
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
            Log.IncludeCallerNames = false;

            WriteLine("Welcome! This is a simple chess bot written in C#.");
            WriteLine();

            var humanSide = GetHumanSide();
            var ai = GetAI();
            var fen = GetStartFen();
            var state = State.ParseFen(fen);
            WriteLine();

            WriteLine($"Playing as: {humanSide}");
            WriteLine();

            Move GetHumanMove()
            {
                while (true)
                {
                    Write("> ");
                    string input = ReadLine().Trim();
                    switch (input.ToLower())
                    {
                        case "exit":
                        case "quit":
                            Commands.ExitCommand();
                            break;
                        case "help":
                            Commands.HelpCommand();
                            break;
                        case "moves":
                            Commands.MovesCommand(state);
                            break;
                        case "searchtimes":
                            Commands.SearchTimesCommand(ai);
                            break;
                        case "undo":
                            state = Commands.UndoCommand(state);
                            break;
                        default:
                            try
                            {
                                var move = Move.Parse(input, state);
                                _ = state.Apply(move); // make sure it's valid
                                return move;
                            }
                            catch (InvalidMoveException e)
                            {
                                WriteLine(e);
                                WriteLine("Sorry, try again.");
                            }
                            break;
                    }
                }
            }

            bool justStarted = true;

            while (true)
            {
                if (justStarted || state.WhiteToMove)
                {
                    WriteLine($"[Turn {state.FullMoveNumber}]");
                    WriteLine();
                }

                WriteLine(Helpers.GetDisplayString(state));
                WriteLine();

                WriteLine($"It's {state.ActiveSide}'s turn.");
                bool humanToMove = (humanSide == state.ActiveSide);
                var nextMove = humanToMove ? GetHumanMove() : ai.PickMove(state);
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
    }

    class AI
    {
        private readonly ISearchAlgorithm _searcher;
        private readonly List<Move> _history;
        private readonly List<TimeSpan> _searchTimes;
        private readonly Stopwatch _sw;

        public List<Move> History => _history;
        public List<TimeSpan> SearchTimes => _searchTimes;

        public AI(ISearchAlgorithm searcher)
        {
            _searcher = searcher;
            _history = new List<Move>();
            _searchTimes = new List<TimeSpan>();
            _sw = new Stopwatch();
        }

        public Move PickMove(State root)
        {
            Debug.Assert(!_sw.IsRunning);
            Debug.Assert(_sw.Elapsed == TimeSpan.Zero);

            _sw.Start();
            var move = _searcher.PickMove(root);
            _sw.Stop();

            _history.Add(move);
            _searchTimes.Add(_sw.Elapsed);
            _sw.Reset();

            return move;
        }
    }
}
