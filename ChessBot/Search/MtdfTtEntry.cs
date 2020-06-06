using System.Diagnostics;

namespace ChessBot.Search
{
    internal readonly struct MtdfTtEntry
    {
        public MtdfTtEntry(int lowerBound, int upperBound)
        {
            Debug.Assert(lowerBound <= upperBound);

            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        public int LowerBound { get; }
        public int UpperBound { get; }

        public override string ToString() => $"[{LowerBound}, {UpperBound}]";
    }
}
