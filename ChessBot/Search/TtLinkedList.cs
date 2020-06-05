namespace ChessBot.Search
{
    /// <summary>
    /// Linked list used to implement transposition tables.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    internal class TtLinkedList<T>
    {
        private readonly TtNode<T> _head;
        private readonly TtNode<T> _tail;

        internal TtLinkedList()
        {
            _head = new TtNode<T>();
            _tail = new TtNode<T>();
            _head.Next = _tail;
            _tail.Previous = _head;
        }

        public void AddToTop(TtNode<T> node)
        {
            node.Next = _head.Next;
            _head.Next.Previous = node;
            node.Previous = _head;
            _head.Next = node;
        }

        public void Remove(TtNode<T> node)
        {
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
            node.Previous = null;
            node.Next = null;
        }

        public TtNode<T> RemoveLru()
        {
            var target = _tail.Previous;
            Remove(target);
            return target;
        }
    }
}
