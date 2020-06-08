using ChessBot.Types;
namespace ChessBot.Search
{
    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    public interface IMovePicker
    {
        Move PickMove(IState state);
    }

    /// <summary>
    /// The interface for search algorithms.
    /// </summary>
    /// <typeparam name="TInfo">The type of additional info computed by the search algorithm.</typeparam>
    public interface IMovePicker<TInfo> : IMovePicker
    {
        Move IMovePicker.PickMove(IState state) => PickMove(state, out _);

        Move PickMove(IState state, out TInfo info);
    }
}
