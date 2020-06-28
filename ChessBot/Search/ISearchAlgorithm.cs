using ChessBot.Search.Tt;
using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Threading;

namespace ChessBot.Search
{
    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    public interface ISearchAlgorithm
    {
        Move PickMove(State root)
        {
            if (root.IsTerminal)
            {
                throw new ArgumentException($"A terminal state was passed to {nameof(PickMove)}", nameof(root));
            }

            var pv = Search(root).Pv;
            Debug.Assert(pv.Length > 0);
            return pv[0];
        }

        string Name { get; }

        int Depth { get; set; }
        int MaxNodes { get; set; }
        ITranspositionTable Tt { get; set; }

        ITranspositionTable MakeTt(int capacity);

        ISearchInfo Search(State root, CancellationToken cancellationToken = default);
    }
}
