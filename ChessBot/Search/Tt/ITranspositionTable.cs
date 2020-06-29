using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    public interface ITranspositionTable
    {
        int Capacity { get; }
    }

    public interface ITranspositionTable<TValue> : ITranspositionTable
    {
        bool Add(ulong key, TValue value);
        bool Touch(ITtReference<TValue> @ref);
        ITtReference<TValue>? TryGetReference(ulong key);
        bool Update(ITtReference<TValue> @ref, TValue newValue);
    }
}
