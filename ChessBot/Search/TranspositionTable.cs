using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;

namespace ChessBot.Search
{
    // todo: it's theoretically possible for 2 states to hash to the same value. should we take care of that?
    /// <summary>
    /// Maps <see cref="State"/> objects to values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class TranspositionTable<T>
    {
        private const int DefaultCapacity = 4096;
        private const int EvictionPeriod = 8;

        private readonly Dictionary<ulong, TtNode<T>> _dict;
        private readonly int _capacity;
        private readonly TtLinkedList<T> _nodes;
        private int _numAdds;

        public TranspositionTable() : this(DefaultCapacity) { }

        public TranspositionTable(int capacity)
        {
            _dict = new Dictionary<ulong, TtNode<T>>(capacity);
            _capacity = capacity;
            _nodes = new TtLinkedList<T>();
            _numAdds = 0;
        }

        public bool Add(State state, T value)
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

            var node = new TtNode<T>(state.Hash, value);
            _dict.Add(state.Hash, node);
            _nodes.AddToTop(node);
            return true;
        }

        public void Touch(TtNode<T> node)
        {
            Debug.Assert(_dict.ContainsKey(node.Key));
            Debug.Assert(_dict[node.Key] == node);

            _nodes.Remove(node);
            _nodes.AddToTop(node);
        }

        public bool TryGetNode(State state, out TtNode<T> node)
            => _dict.TryGetValue(state.Hash, out node);

        // For now, we're using an LRU cache scheme to decide who gets evicted.
        // In the future, we could take other factors into account such as number of hits, relative depth, etc.
        private void Evict()
        {
            var node = _nodes.RemoveLru();
            bool removed = _dict.Remove(node.Key);
            Debug.Assert(removed);
        }
    }
}
