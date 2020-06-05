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
}
