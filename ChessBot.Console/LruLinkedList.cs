namespace ChessBot.Console
{
    public class LruLinkedList
    {
        public LruLinkedList()
        {
            Head = new CacheNode();
            Tail = new CacheNode();
            Head.Next = Tail;
            Tail.Previous = Head;
        }

        public CacheNode Head { get; }
        public CacheNode Tail { get; }

        public void AddToTop(CacheNode node)
        {
            node.Next = Head.Next;
            Head.Next.Previous = node;
            node.Previous = Head;
            Head.Next = node;
        }

        public void Remove(CacheNode node)
        {
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
            node.Previous = null;
            node.Next = null;
        }

        public CacheNode RemoveLru()
        {
            var target = Tail.Previous;
            Remove(target);
            return target;
        }
    }
}
