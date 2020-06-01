using System.Collections.Generic;
using System.Diagnostics;

namespace ChessBot.Console
{
    // todo: it's theoretically possible for 2 states to hash to the same value. we should take care of that
    public class TranspositionCache
    {
        private const int DefaultCapacity = 4096;
        private const int EvictionPeriod = 8;

        private readonly Dictionary<ulong, CacheNode> _dict;
        private readonly LruLinkedList _nodes;
        private readonly int _capacity;
        private int _numAdds;

        public TranspositionCache() : this(DefaultCapacity) { }

        public TranspositionCache(int capacity)
        {
            _dict = new Dictionary<ulong, CacheNode>(capacity);
            _nodes = new LruLinkedList();
            _capacity = capacity;
            _numAdds = 0;
        }

        public bool TryAdd(State state, int value)
        {
            _numAdds++;
            if (_dict.Count == _capacity)
            {
                if ((_numAdds % EvictionPeriod) != 0)
                {
                    return false;
                }
                Evict();
            }

            var node = new CacheNode(state.Hash, value);
            _dict.Add(state.Hash, node);
            _nodes.AddToTop(node);
            return true;
        }

        public bool TryGetValue(State state, out int value)
        {
            if (_dict.TryGetValue(state.Hash, out var node))
            {
                // Since we accessed the node, move it to the top
                //node.Hits++;
                _nodes.Remove(node);
                _nodes.AddToTop(node);

                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        // for now, we're using an lru cache scheme to decide who gets evicted.
        // (todo) we should take other factors into account such as:
        // - how many hits does the entry have
        // - depth relative to GetBestMove() (deeper nodes receive higher prio?)
        // - etc.
        private void Evict()
        {
            var node = _nodes.RemoveLru();
            bool removed = _dict.Remove(node.Key);
            Debug.Assert(removed);
        }
    }
}
