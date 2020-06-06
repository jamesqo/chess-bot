using ChessBot.Types;
namespace ChessBot.Search
{
    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    public interface IMovePicker
    {
        Move PickMove(State state);
    }

    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    /// <typeparam name="TInfo">The type of additional info computed by the search algorithm.</typeparam>
    public interface IMovePicker<TInfo> : IMovePicker
    {
        Move PickMove(State state, out TInfo info);
    }
}
