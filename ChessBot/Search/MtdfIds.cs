﻿using ChessBot.Helpers;
using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Reactive.Subjects;
using System.Threading;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses iterative deepening with MTD-f search to find the best move.
    /// </summary>
    public class MtdfIds : ISearchAlgorithm
    {
        private readonly Mtdf _inner;
        private readonly Subject<ISearchInfo> _iterationCompleted = new Subject<ISearchInfo>();

        public MtdfIds()
        {
            _inner = new Mtdf();
        }

        public string Name => "mtdf-ids";

        public int Depth { get; set; } = 0;
        public int MaxNodes { get; set; } = int.MaxValue;
        public ITranspositionTable Tt
        {
            get => _inner.Tt;
            set => _inner.Tt = value;
        }

        public override string ToString() => $"{Name} depth={Depth} maxNodes={MaxNodes}";

        public IObservable<ISearchInfo> IterationCompleted => _iterationCompleted;

        public ITranspositionTable MakeTt(int capacity) => _inner.MakeTt(capacity);

        public ISearchInfo Search(State root, CancellationToken cancellationToken = default)
        {
            if (Depth <= 0)
            {
                throw new InvalidOperationException($"{nameof(Depth)} wasn't set");
            }

            if (MaxNodes <= 0)
            {
                throw new InvalidOperationException($"Bad value for {nameof(MaxNodes)}");
            }

            Log.Debug("Starting IDS search");

            var pv = ImmutableArray<Move>.Empty;
            int score = 0;
            int nodesSearched = 0;
            int nodesRemaining = MaxNodes;
            var elapsed = TimeSpan.Zero;

            for (int d = 1; d <= Depth; d++)
            {
                Log.Debug("Running mtdf with depth={0}, f={1}", d, score);
                _inner.Depth = d;
                _inner.FirstGuess = score;
                _inner.MaxNodes = nodesRemaining;

                Log.IndentLevel++;
                var icInfo = _inner.Search(root, cancellationToken);
                Log.IndentLevel--;

                nodesSearched += icInfo.NodesSearched;
                nodesRemaining -= icInfo.NodesSearched;
                elapsed += icInfo.Elapsed;

                if (nodesRemaining > 0 && !cancellationToken.IsCancellationRequested)
                {
                    pv = icInfo.Pv;
                    score = icInfo.Score;
                    _iterationCompleted.OnNext(icInfo);
                }
                else
                {
                    break;
                }
            }

            Log.Debug("Finished IDS search");
            return new SearchInfo(Depth, elapsed, nodesSearched, pv, score);
        }
    }
}
