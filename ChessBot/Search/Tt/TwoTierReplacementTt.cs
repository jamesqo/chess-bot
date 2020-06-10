using System;
using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Maps <see cref="IState"/> objects to values of type <typeparamref name="TValue"/>.
    /// Uses a two-tier replacement strategy to decide which node get evicted.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class TwoTierReplacementTt<TValue> : ITranspositionTable<TValue, LruNode<TValue>> where TValue : IHasDepth
    {
        private const int DefaultCapacity = 8192;

        private readonly LruReplacementTt<TValue> _lruTt;
        private readonly DepthReplacementTt<TValue> _depthTt;

        public TwoTierReplacementTt() : this(DefaultCapacity) { }

        public TwoTierReplacementTt(int capacity)
        {
            _lruTt = new LruReplacementTt<TValue>(capacity / 2);
            _depthTt = new DepthReplacementTt<TValue>(capacity / 2);
        }

        public void Add<TState>(TState state, TValue value) where TState : IState
        {
            _lruTt.Add(state, value);
            _depthTt.Add(state, value);
        }

        public bool Touch(LruNode<TValue> node)
        {
            bool touched = _lruTt.Touch(node) || _depthTt.Touch(node);
            Debug.Assert(touched); // for now, we should only be calling this method with nodes that belong to us
            return touched;
        }

        public bool TryGetNode<TState>(TState state, out LruNode<TValue> node) where TState : IState
        {
            // lru-based tt is more likely to contain recent entries, so look there first
            return _lruTt.TryGetNode(state, out node) || _depthTt.TryGetNode(state, out node);
        }
    }
}
