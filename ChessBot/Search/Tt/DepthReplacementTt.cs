using ChessBot.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Maps transpositions to values of type <typeparamref name="TValue"/>.
    /// Evicts nodes with lesser depths first.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class DepthReplacementTt<TValue> : ITranspositionTable<TValue> where TValue : IHasDepth
    {
        private readonly int _capacity;
        private readonly Dictionary<ulong, LruNode<TValue>> _dict;
        private readonly Dictionary<int, LruLinkedList<TValue>> _lists;
        private int? _minDepth; // lowest depth value for which a node is present

        public DepthReplacementTt(int capacity)
        {
            _capacity = capacity;
            _dict = new Dictionary<ulong, LruNode<TValue>>(_capacity);
            _lists = new Dictionary<int, LruLinkedList<TValue>>();
        }

        public int Capacity => _capacity;

        public bool Add(ulong key, TValue value)
        {
            int depth = value.Depth;

            if (_dict.Count == _capacity)
            {
                if (_minDepth != null && depth < _minDepth)
                {
                    // If we're full, don't bother adding nodes below our minimum depth
                    return false;
                }

                Evict();
            }
            else if (_minDepth == null || depth < _minDepth)
            {
                _minDepth = depth;
            }
            EnsureDepth(depth);

            var node = new LruNode<TValue>(key, value);
            if (!_dict.TryAdd(key, node))
            {
                var existingNode = _dict[key];
                Log.Debug("(depth) Evicting node {0} in favor of {1}", existingNode, node);
                Evict(existingNode);
                _dict.Add(key, node);
            }
            _lists[depth].AddToTop(node);
            return true;
        }

        public bool Touch(ITtReference<TValue> @ref)
        {
            if (!(@ref is LruNode<TValue> node) || node.HasExpired) throw new ArgumentException("", nameof(@ref));

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

        public ITtReference<TValue>? TryGetReference(ulong key)
        {
            _dict.TryGetValue(key, out var node);
            return node;
        }

        public bool Update(ITtReference<TValue> @ref, TValue newValue)
        {
            if (!(@ref is LruNode<TValue> node) || node.HasExpired) throw new ArgumentException("", nameof(@ref));

            if (Touch(node))
            {
                var (oldDepth, newDepth) = (node.Value.Depth, newValue.Depth);
                node.Value = newValue;
                // we also have to move the node to the appropriate list if its depth has changed
                if (oldDepth != newDepth)
                {
                    node.Remove();
                    EnsureDepth(newDepth);
                    _lists[newDepth].AddToTop(node);
                }
                return true;
            }
            return false;
        }

        private void Evict()
        {
            Debug.Assert(_minDepth != null);
            Debug.Assert(_lists.ContainsKey((int)_minDepth));

            var targetList = _lists[(int)_minDepth];
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
            if (!_lists.ContainsKey(depth))
            {
                _lists.Add(depth, new LruLinkedList<TValue>());
            }
        }

        private void UpdateMinDepth()
        {
            Debug.Assert(_minDepth != null);
            Debug.Assert(_lists.ContainsKey((int)_minDepth));
            Debug.Assert(_lists[(int)_minDepth].IsEmpty);

            var depthsAscending = _lists.Keys.OrderBy(d => d);
            foreach (int depth in depthsAscending)
            {
                if (!_lists[depth].IsEmpty)
                {
                    Debug.Assert(depth > _minDepth);
                    _minDepth = depth;
                    return;
                }
            }

            // all of the lists are empty
            Debug.Assert(_dict.Count == 0);
            _minDepth = null;
        }
    }
}
