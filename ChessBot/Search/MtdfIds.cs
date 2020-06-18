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

        public MtdfIds(int ttCapacity)
        {
            _inner = new Mtdf(ttCapacity);
        }

        public int Depth { get; set; } = 0;

        public IObservable<ISearchInfo> IterationCompleted => _iterationCompleted;

        public ISearchInfo Search(State root)
        {
            if (Depth <= 0)
            {
                throw new InvalidOperationException("Depth wasn't set");
            }

            Log.Debug("Starting IDS search");

            ImmutableArray<Move> pv = default;
            int score = 0;
            int nodesSearched = 0;
            var elapsed = TimeSpan.Zero;

            for (int d = 1; d <= Depth; d++)
            {
                Log.Debug("Running mtdf with depth={0}, f={1}", d, score);
                _inner.Depth = d;
                _inner.FirstGuess = score;

                Log.IndentLevel++;
                var icInfo = _inner.Search(root);
                Log.IndentLevel--;

                _iterationCompleted.OnNext(icInfo);
                pv = icInfo.Pv;
                score = icInfo.Score;
                nodesSearched += icInfo.NodesSearched;
                elapsed += icInfo.Elapsed;

                if (_stop) break;
            }
            _iterationCompleted.OnCompleted();

            Log.Debug("Finished IDS search");
            return new SearchInfo(Depth, elapsed, nodesSearched, pv, score);
        }

        public void RequestStop()
        {
            _stop = true;
        }
    }
}
