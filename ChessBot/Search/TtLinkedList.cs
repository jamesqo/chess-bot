namespace ChessBot.Search
{
    /// <summary>
    /// Linked list used to implement transposition tables.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    internal class TtLinkedList<TValue>
    {
        private readonly TtNode<TValue> _head;
        private readonly TtNode<TValue> _tail;

        internal TtLinkedList()
        {
            _head = new TtNode<TValue>();
            _tail = new TtNode<TValue>();
            _head.Next = _tail;
            _tail.Previous = _head;
        }

        public void AddToTop(TtNode<TValue> node)
        {
            node.Next = _head.Next;
            _head.Next.Previous = node;
            node.Previous = _head;
            _head.Next = node;
        }

        public void Remove(TtNode<TValue> node)
        {
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
            node.Previous = null;
            node.Next = null;
        }

        public TtNode<TValue> RemoveLru()
        {
            var target = _tail.Previous;
            Remove(target);
            return target;
        }
    }
}
