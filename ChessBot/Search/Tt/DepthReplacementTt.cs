using ChessBot.Helpers;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Maps <see cref="IState"/> objects to values of type <typeparamref name="TValue"/>.
    /// Evicts nodes with lesser depths first.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class DepthReplacementTt<TValue> : ITranspositionTable<TValue, LruNode<TValue>> where TValue : IHasDepth
    {
        private const int DefaultCapacity = 4096;

        private readonly Dictionary<ulong, LruNode<TValue>> _dict;
        private readonly int _capacity;
        private readonly List<LruLinkedList<TValue>> _lists;

        public DepthReplacementTt() : this(DefaultCapacity) { }

        public DepthReplacementTt(int capacity)
        {
            _dict = new Dictionary<ulong, LruNode<TValue>>(capacity);
            _capacity = capacity;
            _lists = new List<LruLinkedList<TValue>>();
        }

        public void Add<TState>(TState state, TValue value) where TState : IState
        {
            if (_dict.Count == _capacity)
            {
                Evict();
            }
            EnsureDepth(value.Depth);

            var node = new LruNode<TValue>(state.Hash, value);
            if (!_dict.TryAdd(state.Hash, node))
            {
                var existingNode = _dict[state.Hash];
                Log.Debug("Evicting node {0} in favor of {1}", existingNode, node);
                Evict(existingNode);
                _dict.Add(state.Hash, node);
            }
            _lists[value.Depth].AddToTop(node);
        }

        public bool Touch(LruNode<TValue> node)
        {
            if (_dict.TryGetValue(node.Key, out var dictNode) && ReferenceEquals(node, dictNode))
            {
                Debug.Assert(_lists.Count > node.Value.Depth);

                Log.Debug("Node {0} was hit, moving to top of cache", node);
                node.Remove();
                _lists[node.Value.Depth].AddToTop(node);
                return true;
            }

            return false; // node isn't from here or got evicted
        }

        public bool TryGetNode<TState>(TState state, out LruNode<TValue> node) where TState : IState
        {
            return _dict.TryGetValue(state.Hash, out node);
        }

        private void Evict()
        {
            for (int depth = 0; depth < _lists.Count; depth++)
            {
                var list = _lists[depth];
                if (!list.IsEmpty)
                {
                    var lru = list.Lru;
                    Log.Debug("Evicting lru node {0} for depth={1}", lru, depth);
                    Evict(lru);
                    return;
                }
            }
        }

        private void Evict(LruNode<TValue> node)
        {
            node.Remove();
            bool removed = _dict.Remove(node.Key);
            Debug.Assert(removed);
        }

        private void EnsureDepth(int depth)
        {
            Debug.Assert(depth >= 0);

            if (_lists.Count <= depth)
            {
                int delta = (depth + 1) - _lists.Count;
                for (int i = 0; i < delta; i++)
                {
                    _lists.Add(new LruLinkedList<TValue>());
                }
            }

            Debug.Assert(_lists.Count > depth);
        }
    }
}
