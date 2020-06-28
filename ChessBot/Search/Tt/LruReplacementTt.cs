using ChessBot.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Maps transpositions to values of type <typeparamref name="TValue"/>. Uses an LRU eviction policy.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class LruReplacementTt<TValue> : ITranspositionTable<TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<ulong, LruNode<TValue>> _dict;
        private readonly LruLinkedList<TValue> _nodes;

        public LruReplacementTt(int capacity)
        {
            _capacity = capacity;
            _dict = new Dictionary<ulong, LruNode<TValue>>(_capacity);
            _nodes = new LruLinkedList<TValue>();
        }

        public int Capacity => _capacity;

        public bool Add(ulong key, TValue value)
        {
            if (_dict.Count == _capacity)
            {
                Evict();
            }

            var node = new LruNode<TValue>(key, value);
            if (!_dict.TryAdd(key, node))
            {
                // although rare, this could happen if a state is not in the table during the initial lookup, but is
                // populated during a recursive call as it searches its children. afterwards, there will be a conflict
                // when it tries to call Add() with an existing key. our behavior is to favor the newer entry since it
                // probably contains information about a greater depth.
                //
                // this could also theoretically happen in the case of a hash collision, although that's very unlikely.
                var existingNode = _dict[key];
                Log.Debug("(lru) Evicting node {0} in favor of {1}", existingNode, node);
                Evict(existingNode);
                _dict.Add(key, node);
            }
            _nodes.AddToTop(node);
            return true;
        }

        public bool Touch(ITtReference<TValue> @ref)
        {
            if (!(@ref is LruNode<TValue> node) || node.HasExpired) throw new ArgumentException("", nameof(@ref));

            if (_dict.TryGetValue(node.Key, out var dictNode) && ReferenceEquals(node, dictNode))
            {
                Log.Debug("(lru) Node {0} was hit, moving to top of cache", node);
                node.Remove();
                _nodes.AddToTop(node);
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
                node.Value = newValue;
                return true;
            }
            return false;
        }

        // For now, we're using an LRU cache scheme to decide who gets evicted.
        // In the future, we could take other factors into account such as number of hits, relative depth, etc.
        private void Evict()
        {
            var lru = _nodes.Lru;
            Log.Debug("(lru) Evicting lru node {0}", lru);
            Evict(lru);
        }

        private void Evict(LruNode<TValue> node)
        {
            node.Remove();
            bool removed = _dict.Remove(node.Key);
            Debug.Assert(removed);
        }
    }
}
