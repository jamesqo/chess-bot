using ChessBot.Types;

namespace ChessBot.Search
{
    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    public interface ISearchAlgorithm
    {
        Move PickMove(State root) => Search(root).Pv[0];

        ISearchInfo Search(State root);
    }
}
