using ChessBot.Types;
using System;
using System.Collections.Immutable;

namespace ChessBot.Search
{
    public class SearchInfo : ISearchInfo
    {
        public SearchInfo(int depth, TimeSpan elapsed, int nodesSearched, ImmutableArray<Move> pv, int score)
        {
            Depth = depth;
            Elapsed = elapsed;
            NodesSearched = nodesSearched;
            Pv = pv;
            Score = score;
        }

        public int Depth { get; }
        public TimeSpan Elapsed { get; }
        public int NodesSearched { get; }
        public ImmutableArray<Move> Pv { get; }
        public int Score { get; }
    }
}
