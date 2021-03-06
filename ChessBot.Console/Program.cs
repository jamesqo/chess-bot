﻿using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Console;

namespace ChessBot.Console
{
    class Program
    {
        const int SearchDepth = 7;
        const int TtCapacity = 1 << 16;

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

        // todo: the user should be able to change the search algorithm during the course of the program, as well as other parameters like depth, TT capacity, etc
        static AI GetAI()
        {
            ISearchAlgorithm inner;
            while (true)
            {
                Write("Pick AI strategy [mtdf, mtdf-ids (default)]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    case "mtdf":
                        inner = new Mtdf() { Depth = SearchDepth };
                        inner.Tt = inner.MakeTt(TtCapacity);
                        break;
                    case "":
                    case "mtdf-ids":
                        inner = new MtdfIds() { Depth = SearchDepth };
                        inner.Tt = inner.MakeTt(TtCapacity);
                        break;
                    default:
                        continue;
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
            Log.Enabled = true;
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
                        case "":
                            break;
                        case "exit":
                        case "quit":
                            Commands.Exit();
                            break;
                        case "help":
                            Commands.Help();
                            break;
                        case "moves":
                            Commands.Moves(state);
                            break;
                        case "searchinfo":
                            Commands.SearchInfo(ai);
                            break;
                        case "undo":
                            state = Commands.Undo(state);
                            break;
                        default:
                            try
                            {
                                var move = Move.Parse(input, state);
                                _ = state.Apply(move); // make sure it's valid
                                return move;
                            }
                            catch (InvalidMoveException)
                            {
                                WriteLine("Didn't recognize that! Sorry, try again.");
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

        // todo: We should output the board again right before we exit
        static void CheckForEnd(State state)
        {
            if (state.IsCheckmate)
            {
                WriteLine($"{state.OpposingSide} wins!");
                Environment.Exit(0);
            }

            if (state.IsStalemate)
            {
                WriteLine("It's a draw.");
                Environment.Exit(0);
            }

            // todo: Check for draw by 3-fold repetition, 50-move rule, etc.
        }
    }

    class AI
    {
        private readonly ISearchAlgorithm _searcher;
        private readonly List<Move> _history;
        private readonly List<ISearchInfo> _searchInfos;

        public List<Move> History => _history;
        public List<ISearchInfo> SearchInfos => _searchInfos;

        public AI(ISearchAlgorithm searcher)
        {
            _searcher = searcher;
            _history = new List<Move>();
            _searchInfos = new List<ISearchInfo>();
        }

        public Move PickMove(State root)
        {
            var info = _searcher.Search(root);
            var move = info.Pv[0];

            _history.Add(move);
            _searchInfos.Add(info);

            return move;
        }
    }
}
