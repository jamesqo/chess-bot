using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;

namespace ChessBot.Uci
{
    class Program
    {
        const int EntriesPerMb = (1 << 14); // todo

        readonly Options _options = new Options();

        State _root;
        MtdfIds _searcher;
        volatile bool _ponder = false;
        volatile bool _stop = false;

        volatile bool _searchInProgress = false;
        readonly ConcurrentQueue<Task> _searchQueue = new ConcurrentQueue<Task>();

        void Uci()
        {
            WriteLine("id name chessbot_uci 1.0.0");
            WriteLine("id author James Ko");

            foreach (var option in _options)
            {
                Write($"option name {option.Name} type {option.Type} default {option.DefaultValue}");
                if (option.Min != null) Write($" min {option.Min}");
                if (option.Max != null) Write($" max {option.Max}");
                WriteLine();
            }

            WriteLine("uciok");
        }

        void IsReady()
        {
            WriteLine("readyok");
        }

        void SetOption(Stack<string> tokens)
        {
            string name = null, value = null;

            while (tokens.TryPop(out var paramName))
            {
                switch (paramName)
                {
                    case "name":
                        name = tokens.Pop();
                        break;
                    case "value":
                        value = tokens.Pop();
                        break;
                }
            }

            if (name == null || value == null || !_options[name].TryParse(value, out object valueObj))
            {
                // todo: throw error
                return;
            }

            _options[name].Value = valueObj;
        }

        void UciNewGame()
        {
            _root = null;
            _searcher = null;
        }

        void Position(Stack<string> tokens)
        {
            // todo: better error handling here

            string fen = "";
            string token = tokens.Pop();
            switch (token)
            {
                case "startpos":
                    fen = State.StartFen;
                    tokens.TryPop(out _); // pop moves token
                    break;
                case "fen":
                    fen = tokens.Pop();
                    while (tokens.TryPop(out token) && token != "moves")
                        fen += ' ' + token;
                    break;
                default:
                    // todo: error
                    break;
            }
            _root = State.ParseFen(fen);

            while (tokens.TryPop(out token))
            {
                // todo: castling moves are sent in the form of king "takes" his own rook. account for these
                _root = _root.Apply(Move.ParseLong(token));
            }
        }

        void Go(Stack<string> tokens)
        {
            if (_root == null)
            {
                // todo: error message
                return;
            }

            var settings = new GoSettings();
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
                        settings.SearchMoves = searchMoves.ToImmutableArray();
                        break;
                    case "ponder":
                        _ponder = true;
                        break;
                    case "depth":
                        settings.Depth = int.Parse(tokens.Pop());
                        break;
                    case "nodes":
                        settings.Nodes = int.Parse(tokens.Pop());
                        break;
                    case "mate": // todo
                        settings.Mate = int.Parse(tokens.Pop());
                        break;
                    case "movetime": // todo
                        settings.MoveTime = TimeSpan.FromMilliseconds(int.Parse(tokens.Pop()));
                        break;
                    case "infinite":
                        settings.Infinite = true;
                        break;
                }
            }

            if (settings.Depth == null && settings.Nodes == null && !settings.Infinite)
            {
                // error: one of depth, nodes, or infinite must be specified
                return;
            }

            _searcher ??= new MtdfIds();
            _searcher.TtCapacity = (int)_options["Hash"].Value * EntriesPerMb;

            // reset all relevant state variables
            _ponder = false;
            _stop = false;

            // define the task and either run it straight away, or add it to the queue

            // we don't want later changes to these fields to be picked up on by the task.
            // eg. "go / go / ucinewgame" should not use a different searcher for the second task.
            var (rootCopy, searcherCopy) = (_root, _searcher);
            var searchTask = new Task(() =>
            {
                searcherCopy.Depth = settings.Depth ?? int.MaxValue;
                searcherCopy.MaxNodes = settings.Nodes ?? int.MaxValue;

                var disp = searcherCopy.IterationCompleted.Subscribe(icInfo =>
                {
                    // todo: even if we have cached tt info, we should be outputting the full pv
                    // todo: other info fields (see stockfish)
                    var output = $"info depth {icInfo.Depth} time {(int)icInfo.Elapsed.TotalMilliseconds} nodes {icInfo.NodesSearched} score cp {icInfo.Score} pv {string.Join(' ', icInfo.Pv)}";
                    WriteLine(output);
                    if (_stop)
                    {
                        searcherCopy.Stop();
                    }
                });

                var info = searcherCopy.Search(rootCopy);
                disp.Dispose();

                // if we finished but we're in ponder or infinite mode, wait until we receive "ponderhit" or "stop"
                while (!_stop && (settings.Infinite || _ponder))
                {
                    Thread.Sleep(500);
                }

                // output the best move. if there's not a mate in 1 and we searched more than depth 1, output that too
                // as the next move we expect the user to play.

                var output = $"bestmove {info.Pv[0]}";
                if (info.Pv.Length > 1)
                {
                    output += $" ponder {info.Pv[1]}";
                }
                WriteLine(output);

                // run the next task
                _searchInProgress = _searchQueue.TryDequeue(out var next);
                next?.Start();
            });

            if (!_searchInProgress)
            {
                _searchInProgress = true;
                searchTask.Start();
            }
            else _searchQueue.Enqueue(searchTask);
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
