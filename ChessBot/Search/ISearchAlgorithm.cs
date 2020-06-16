using ChessBot.Types;
namespace ChessBot.Search
{
    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    public interface ISearchAlgorithm
    {
        Move PickMove(State root);
    }

    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    /// <typeparam name="TInfo">The type of additional info computed by the search algorithm.</typeparam>
    public interface ISearchAlgorithm<TInfo> : ISearchAlgorithm
    {
        Move ISearchAlgorithm.PickMove(State root) => PickMove(root, out _);

        Move PickMove(State root, out TInfo info);
    }
}
