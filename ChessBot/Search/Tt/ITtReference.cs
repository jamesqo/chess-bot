namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Holds a reference to a value in a transposition table.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public interface ITtReference<out TValue>
    {
        public TValue Value { get; }
        public bool HasExpired { get; }
    }
}
