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
        private int _minDepth; // lowest depth value for which a node is present

        public DepthReplacementTt() : this(DefaultCapacity) { }

        public DepthReplacementTt(int capacity)
        {
            _dict = new Dictionary<ulong, LruNode<TValue>>(capacity);
            _capacity = capacity;
            _lists = new List<LruLinkedList<TValue>>();
            _minDepth = -1;
        }

        public bool Add<TState>(TState state, TValue value) where TState : IState
        {
            int depth = value.Depth;
            Debug.Assert(depth >= 0);

            if (_dict.Count == _capacity)
            {
                if (depth < _minDepth)
                {
                    return false;
                }

                Evict();
            }
            else if (_minDepth == -1 || depth < _minDepth)
            {
                _minDepth = depth;
            }
            EnsureDepth(depth);

            var node = new LruNode<TValue>(state.Hash, value);
            if (!_dict.TryAdd(state.Hash, node))
            {
                var existingNode = _dict[state.Hash];
                Log.Debug("(depth) Evicting node {0} in favor of {1}", existingNode, node);
                Evict(existingNode);
                _dict.Add(state.Hash, node);
            }
            _lists[depth].AddToTop(node);
            return true;
        }

        public bool Touch(LruNode<TValue> node)
        {
            if (_dict.TryGetValue(node.Key, out var dictNode) && ReferenceEquals(node, dictNode))
            {
                Debug.Assert(_lists.Count > node.Value.Depth);

                Log.Debug("(depth) Node {0} was hit, moving to top of cache", node);
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
            Debug.Assert(_minDepth >= 0 && _minDepth < _lists.Count);

            var targetList = _lists[_minDepth];
            var target = targetList.Lru;
            Log.Debug("(depth) Evicting lru node {0} for depth={1}", target, _minDepth);
            Evict(target);
            if (targetList.IsEmpty) UpdateMinDepth();
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

        private void UpdateMinDepth()
        {
            Debug.Assert(_minDepth >= 0 && _minDepth < _lists.Count);
            Debug.Assert(_lists[_minDepth].IsEmpty);

            for (int depth = (_minDepth + 1); depth < _lists.Count; depth++)
            {
                if (!_lists[depth].IsEmpty)
                {
                    _minDepth = depth;
                    return;
                }
            }

            // all of the lists are empty
            Debug.Assert(_dict.Count == 0);
            _minDepth = -1;
        }
    }
}
