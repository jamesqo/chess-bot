using ChessBot.Types;
using System;

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

            return Search(root).Pv[0];
        }

        string Name { get; }
        int Depth { get; }
        int MaxNodes { get; }

        ISearchInfo Search(State root);
    }
}
