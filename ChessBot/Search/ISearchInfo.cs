using ChessBot.Types;
using System;
using System.Collections.Immutable;

namespace ChessBot.Search
{
    public interface ISearchInfo
    {
        int Depth { get; }
        TimeSpan Elapsed { get; }
        int NodesSearched { get; }
        ImmutableArray<Move> Pv { get; }
        int Score { get; }
    }
}
