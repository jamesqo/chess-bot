using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// An entry in the transposition table.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class TtNode<T>
    {
        // dummy initializer for head and tail
        internal TtNode()
        {
        }

        internal TtNode(ulong key, T value)
        {
            Key = key;
            Value = value;
        }

        public ulong Key { get; }
        public T Value { get; set; }
        public TtNode<T> Previous { get; internal set; }
        public TtNode<T> Next { get; internal set; }

        internal bool WasEvicted => Previous == null && Next == null;

        public override string ToString() => $"{nameof(Key)} = {Key}, {nameof(Value)} = {Value}";
    }
}
