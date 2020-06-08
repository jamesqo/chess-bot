namespace ChessBot.Search
{
    /// <summary>
    /// An entry in the transposition table.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class TtNode<TValue>
    {
        // dummy initializer for head and tail
        internal TtNode()
        {
        }

        internal TtNode(ulong key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public ulong Key { get; }
        public TValue Value { get; set; }
        public TtNode<TValue> Previous { get; internal set; }
        public TtNode<TValue> Next { get; internal set; }

        internal bool WasEvicted => Previous == null && Next == null;

        public override string ToString() => $"{nameof(Key)} = {Key}, {nameof(Value)} = {Value}";
    }
}
