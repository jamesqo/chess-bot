namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Linked list used to implement LRU transposition tables.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    internal class LruLinkedList<TValue>
    {
        private readonly LruNode<TValue> _head;
        private readonly LruNode<TValue> _tail;

        internal LruLinkedList()
        {
            _head = new LruNode<TValue>();
            _tail = new LruNode<TValue>();
            _head.Next = _tail;
            _tail.Previous = _head;
        }

        public bool IsEmpty => _tail.Previous == _head;

        public LruNode<TValue> Lru => _tail.Previous;

        public void AddToTop(LruNode<TValue> node)
        {
            node.Next = _head.Next;
            _head.Next.Previous = node;
            node.Previous = _head;
            _head.Next = node;
        }
    }
}
