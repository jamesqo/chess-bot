using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    public interface ITranspositionTable<TValue>
    {
        int Capacity { get; }

        bool Add(ulong key, TValue value);
        bool Touch(ITtReference<TValue> @ref);
        ITtReference<TValue>? TryGetReference(ulong key);
        bool Update(ITtReference<TValue> @ref, TValue newValue);
    }

    public static class ITranspositionTableExtensions
    {
        public static void UpdateOrAdd<TValue>(this ITranspositionTable<TValue> tt, ITtReference<TValue> @ref, ulong key, TValue value)
        {
            if (@ref != null && !@ref.HasExpired)
            {
                bool updated = tt.Update(@ref, value);
                Debug.Assert(updated);
            }
            else
            {
                tt.Add(key, value);
            }
        }
    }
}
