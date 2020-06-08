using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;

namespace ChessBot.Search
{
    // todo: it's theoretically possible for 2 states to hash to the same value. should we take care of that?
    /// <summary>
    /// Maps <see cref="IState"/> objects to values of type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class TranspositionTable<TValue>
    {
        private const int DefaultCapacity = 4096;
        private const int EvictionPeriod = 8;

        private readonly Dictionary<ulong, TtNode<TValue>> _dict;
        private readonly int _capacity;
        private readonly TtLinkedList<TValue> _nodes;
        private int _numAdds;

        public TranspositionTable() : this(DefaultCapacity) { }

        public TranspositionTable(int capacity)
        {
            _dict = new Dictionary<ulong, TtNode<TValue>>(capacity);
            _capacity = capacity;
            _nodes = new TtLinkedList<TValue>();
            _numAdds = 0;
        }

        public bool Add<TState>(TState state, TValue value) where TState : IState
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

            var node = new TtNode<TValue>(state.Hash, value);
            _dict.Add(state.Hash, node);
            _nodes.AddToTop(node);
            return true;
        }

        public void Touch(TtNode<TValue> node)
        {
            Debug.Assert(_dict.ContainsKey(node.Key));
            Debug.Assert(_dict[node.Key] == node);

            _nodes.Remove(node);
            _nodes.AddToTop(node);
        }

        public bool TryGetNode<TState>(TState state, out TtNode<TValue> node) where TState : IState
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
