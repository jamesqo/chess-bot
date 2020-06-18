using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using static System.Console;

namespace ChessBot.Uci
{
    class Program
    {
        // todo: synchronize accesses to the console w/ a lock?

        State _root;
        volatile bool _ponder;
        volatile bool _stop;

        void Uci()
        {
            WriteLine("id name chessbot_uci 1.0.0");
            WriteLine("id author James Ko");
            // todo: write options
            WriteLine("uciok");
        }

        void IsReady()
        {
            WriteLine("readyok");
        }

        void SetOption(Stack<string> tokens)
        {
            // todo: set the option
        }

        void UciNewGame() => _root = null;

        void Position(Stack<string> tokens)
        {
            var fen = tokens.Pop();
            if (fen == "startpos") fen = State.StartFen;
            _root = State.ParseFen(fen);
            if (tokens.TryPop(out var movesToken) && movesToken == "moves")
            {
                while (tokens.TryPop(out var move))
                {
                    _root = _root.Apply(Move.ParseLong(move));
                }
            }
        }

        void Go(Stack<string> tokens)
        {
            if (_root == null)
            {
                // todo: error message
                return;
            }

            // todo: if we have an ongoing search, kill it

            var paras = new GoParams();
            while (tokens.TryPop(out var paramName))
            {
                switch (paramName)
                {
                    case "searchmoves": // needs to be the last command on the line
                        var searchMoves = new List<Move>();
                        while (tokens.TryPop(out var move))
                        {
                            searchMoves.Add(Move.ParseLong(move));
                        }
                        paras.SearchMoves = searchMoves.ToImmutableArray();
                        break;
                    case "ponder":
                        paras.Ponder = true;
                        break;
                    case "depth":
                        paras.Depth = int.Parse(tokens.Pop());
                        break;
                    case "nodes":
                        paras.Nodes = int.Parse(tokens.Pop());
                        break;
                    case "mate":
                        paras.Mate = int.Parse(tokens.Pop());
                        break;
                    case "movetime":
                        paras.MoveTime = TimeSpan.FromMilliseconds(int.Parse(tokens.Pop()));
                        break;
                    case "infinite":
                        paras.Infinite = true;
                        break;
                }
            }
            // start the search
            Task.Run(() =>
            {
                var searcher = new MtdfIds(ttCapacity: (1 << 16));
                //searcher.Depth = options.Depth ?? ...;
                //searcher.MaxNodes = options.Nodes;
                searcher.IterationCompleted.Subscribe(icInfo =>
                {
                    var output = $"info depth {icInfo.Depth} time {icInfo.Elapsed} nodes {icInfo.NodesSearched} pv {icInfo.Pv} score cp {icInfo.Score}";
                    WriteLine(output);
                    if (_stop)
                    {
                        searcher.RequestStop();
                    }
                });

                var info = searcher.Search(_root);

                // if infinite is true, then busy wait once we reach the max depth, or just keep searching if none.
                // what if neither depth nor infinite is spec'd, eg. "go"?
                while (!_stop && (paras.Infinite || _ponder)) { } // if we finished but we're in ponder or infinite mode, busy wait until we received "ponderhit"

                // output the bestmove. if there's not a mate in 1 and we searched more than depth 1, output that too

                var output = $"bestmove {info.Pv[0]}";
                if (info.Pv.Length > 1)
                {
                    output += $" ponder {info.Pv[1]}";
                }
                WriteLine(output);
            });
        }

        void PonderHit()
        {
            // move that was played matched the move that we were pondering
            _ponder = false;
        }

        void Stop()
        {
            _stop = true;
        }

        void Quit()
        {
            Environment.Exit(0);
        }

        void Run(string[] args)
        {
            while (true)
            {
                string command = ReadLine().Trim();
                var tokens = new Stack<string>(command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Reverse());

                if (tokens.Count == 0) // newline
                {
                    continue;
                }

                switch (tokens.Pop())
                {
                    case "uci":
                        Uci();
                        break;
                    case "isready":
                        IsReady();
                        break;
                    case "setoption":
                        SetOption(tokens);
                        break;
                    case "ucinewgame":
                        UciNewGame();
                        break;
                    case "position":
                        Position(tokens);
                        break;
                    case "go":
                        Go(tokens);
                        break;
                    case "ponderhit":
                        PonderHit();
                        break;
                    case "stop":
                        Stop();
                        break;
                    case "quit":
                        Quit();
                        break;
                }
            }
        }

        static void Main(string[] args) => new Program().Run(args);
    }
}
