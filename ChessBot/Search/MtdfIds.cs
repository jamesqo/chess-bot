using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Reactive.Subjects;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses iterative deepening with MTD-f search to find the best move.
    /// </summary>
    public class MtdfIds : ISearchAlgorithm
    {
        private readonly Mtdf _inner;
        private readonly Subject<ISearchInfo> _iterationCompleted = new Subject<ISearchInfo>();
        private bool _stop = false;

        public MtdfIds()
        {
            _inner = new Mtdf();
        }

        public string Name => "mtdf-ids";

        public int Depth { get; set; } = 0;
        public int MaxNodes { get; set; } = int.MaxValue;
        public int TtCapacity
        {
            get => _inner.TtCapacity;
            set => _inner.TtCapacity = value;
        }

        public override string ToString() => $"{Name} depth={Depth} maxNodes={MaxNodes}";

        public IObservable<ISearchInfo> IterationCompleted => _iterationCompleted;

        public ISearchInfo Search(State root)
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

            _stop = false;

            ImmutableArray<Move> pv = default;
            int score = 0;
            int nodesSearched = 0;
            int remainingNodes = MaxNodes;
            var elapsed = TimeSpan.Zero;

            for (int d = 1; d <= Depth && !_stop && remainingNodes > 0; d++)
            {
                Log.Debug("Running mtdf with depth={0}, f={1}", d, score);
                _inner.Depth = d;
                _inner.FirstGuess = score;
                _inner.MaxNodes = remainingNodes;

                Log.IndentLevel++;
                var icInfo = _inner.Search(root);
                Log.IndentLevel--;

                _iterationCompleted.OnNext(icInfo);
                pv = icInfo.Pv;
                score = icInfo.Score;
                nodesSearched += icInfo.NodesSearched;
                remainingNodes -= icInfo.NodesSearched;
                elapsed += icInfo.Elapsed;
            }

            Log.Debug("Finished IDS search");
            return new SearchInfo(Depth, elapsed, nodesSearched, pv, score);
        }

        public void Stop()
        {
            _stop = true;
        }
    }
}
