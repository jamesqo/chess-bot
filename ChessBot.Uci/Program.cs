using ChessBot.Search;
using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ChessBot.Uci.ConsoleWrapper;

namespace ChessBot.Uci
{
    class Program
    {
        const int EntriesPerMb = (1 << 12);
        const int DefaultSearchDepth = 7;

        readonly Options _options = new Options();
        readonly ConcurrentQueue<Task> _searchQueue = new ConcurrentQueue<Task>();

        // these fields correspond to the most recently requested search
        State _root;
        ITranspositionTable _tt;
        
        // these fields correspond to the ongoing search
        volatile CancellationTokenSource _cts;
        volatile bool _ponder = false;
        volatile bool _searchInProgress = false;

        void Uci()
        {
            WriteLine("id name chessbot_uci 1.0.0");
            WriteLine("id author James Ko");

            foreach (var option in _options)
            {
                string desc = $"option name {option.Name} type {option.Type} default {option.DefaultValue}";
                if (option.Min != null) desc += $" min {option.Min}";
                if (option.Max != null) desc += $" max {option.Max}";
                WriteLine(desc);
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
                    default:
                        Error.WriteLine($"Unrecognized parameter: {paramName}");
                        return;
                }
            }

            if (name == null || value == null)
            {
                Error.WriteLine("Name and value must be specified");
                return;
            }

            if (!_options.TrySet(name, value))
            {
                Error.WriteLine($"Could not set {name}={value}");
                return;
            }
        }

        void UciNewGame()
        {
            _root = null;
            _tt = null;
        }

        void Position(Stack<string> tokens)
        {
            // todo: better error handling here

            string fen;
            string token = tokens.Pop();
            switch (token)
            {
                case "startpos":
                    fen = State.StartFen;
                    tokens.TryPop(out _); // pop moves token, if any
                    break;
                case "fen":
                    fen = tokens.Pop();
                    while (tokens.TryPop(out token) && token != "moves")
                        fen += ' ' + token;
                    break;
                default:
                    Error.WriteLine($"Unrecognized parameter: {token}");
                    return;
            }
            _root = State.ParseFen(fen);

            while (tokens.TryPop(out token))
            {
                _root = _root.Apply(Move.ParseLong(token));
            }
        }

        void Go(Stack<string> tokens)
        {
            if (_root == null)
            {
                Error.WriteLine("Position not set, use the 'position' command to set it");
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

            // go ahead with the search

            var searcher = new MtdfIds();
            searcher.Depth = settings.Depth ?? ((settings.Infinite || settings.Nodes != null) ? int.MaxValue : DefaultSearchDepth);
            searcher.MaxNodes = settings.Nodes ?? int.MaxValue;

            int ttCapacity = _options.Get<int>("Hash") * EntriesPerMb;
            if (_tt == null || _tt.Capacity != ttCapacity)
            {
                _tt = searcher.MakeTt(ttCapacity);
            }
            searcher.Tt = _tt;

            _ponder = false;
            var cts = new CancellationTokenSource();

            // define the task and either run it straight away, or add it to the queue if there's one in progress

            var rootCopy = _root; // if a new search is requested before this one is started, we don't want to pick up on a new position
            var searchTask = new Task(() =>
            {
                try
                {
                    _cts = cts;
                    Search(rootCopy, searcher, settings, this, cts.Token);
                }
                // exceptions don't propagate to the main thread unless they are explicitly handled like this
                catch (Exception e)
                {
                    Error.WriteLine(e);
                }
                finally
                {
                    cts.Dispose();
                    _cts = null;

                    // run the next task if one is queued

                    if (_searchQueue.TryDequeue(out var nextTask))
                    {
                        nextTask.Start();
                    }
                    else
                    {
                        _searchInProgress = false;
                    }
                }
            });

            if (!_searchInProgress)
            {
                Debug.Assert(_searchQueue.IsEmpty);
                _searchInProgress = true;
                searchTask.Start();
            }
            else _searchQueue.Enqueue(searchTask);
        }

        static void Search(
            State root,
            MtdfIds searcher,
            GoSettings settings,
            Program program,
            CancellationToken ct)
        {
            ISearchInfo info;

            {
                using var _ = searcher.IterationCompleted.Subscribe(icInfo =>
                {
                    // todo: even if we have cached tt info, we should be outputting the full pv
                    // todo: output other info fields (see stockfish)
                    var output = $"info depth {icInfo.Depth} time {(int)icInfo.Elapsed.TotalMilliseconds} nodes {icInfo.NodesSearched} score cp {icInfo.Score} pv {string.Join(' ', icInfo.Pv)}";
                    WriteLine(output);
                });

                info = searcher.Search(root, ct);
            }

            // if we finished but we're in ponder or infinite mode, wait until we receive "ponderhit" or "stop"

            while (!ct.IsCancellationRequested && (settings.Infinite || program._ponder))
            {
                Thread.Sleep(500);
            }

            // output the best move. if the pv contains more than 1 move (eg. there's not a mate in 1 and we searched more than depth 1),
            // output that too as the next move we expect the user to play.

            Debug.Assert(!info.Pv.IsEmpty);
            var output = $"bestmove {info.Pv[0]}";
            if (info.Pv.Length > 1)
            {
                output += $" ponder {info.Pv[1]}";
            }
            WriteLine(output);
        }

        void PonderHit()
        {
            // move that was played by the user matched the move that we were pondering
            _ponder = false;
        }

        void Stop()
        {
            if (_cts == null)
            {
                //Error.WriteLine("No search in progress");
                return;
            }

            _cts.Cancel();
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
