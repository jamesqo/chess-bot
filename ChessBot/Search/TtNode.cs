using ChessBot.Helpers;

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

        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire();
            sb.Append(nameof(Key));
            sb.Append(" = ");
            sb.Append(Key);
            sb.Append(", ");
            sb.Append(nameof(Value));
            sb.Append(" = ");
            sb.Append(Value.ToString());
            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
