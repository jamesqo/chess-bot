using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Maps transpositions to values of type <typeparamref name="TValue"/>.
    /// Uses a two-tier replacement strategy to decide which nodes get evicted.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class TwoTierReplacementTt<TValue> : ITranspositionTable<TValue> where TValue : IHasDepth
    {
        private const int DefaultCapacity = 8192;

        private readonly LruReplacementTt<TValue> _lruTt;
        private readonly DepthReplacementTt<TValue> _depthTt;

        public TwoTierReplacementTt() : this(DefaultCapacity) { }

        public TwoTierReplacementTt(int? capacity = null)
        {
            int cap = capacity ?? DefaultCapacity;
            _lruTt = new LruReplacementTt<TValue>(cap / 2);
            _depthTt = new DepthReplacementTt<TValue>(cap / 2);
        }

        public bool Add(ulong key, TValue value)
        {
            // NOTE: we're using | and not || because we want to add to both of them
            bool added = _lruTt.Add(key, value) | _depthTt.Add(key, value);
            Debug.Assert(added); // lru replacement should always add new states
            return added;
        }

        public bool Touch(ITtReference<TValue> @ref)
        {
            bool touched = _lruTt.Touch(@ref) || _depthTt.Touch(@ref);
            Debug.Assert(touched);
            return touched;
        }

        public ITtReference<TValue>? TryGetReference(ulong key)
        {
            // lru-based tt is more likely to contain recent entries, so look there first
            return _lruTt.TryGetReference(key) ?? _depthTt.TryGetReference(key);
        }

        public bool Update(ITtReference<TValue> @ref, TValue newValue)
        {
            return _lruTt.Update(@ref, newValue) || _depthTt.Update(@ref, newValue);
        }
    }
}
