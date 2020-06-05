namespace ChessBot.Search
{
    /// <summary>
    /// An entry in the transposition table.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    internal class TtNode<T>
    {
        public ulong Key { get; }
        public T Value { get; }
        public TtNode<T> Previous { get; set; }
        public TtNode<T> Next { get; set; }

        // dummy initializer for head and tail
        internal TtNode()
        {
        }

        internal TtNode(ulong key, T value)
        {
            Key = key;
            Value = value;
        }
    }
}
