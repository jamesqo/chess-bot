using System;

namespace ChessBot.Types
{
    public enum Rank
    {
        Rank1,
        Rank2,
        Rank3,
        Rank4,
        Rank5,
        Rank6,
        Rank7,
        Rank8,
    }

    public static class RankHelpers
    {
        // note: we leave it up to the caller to check for validity
        public static Rank FromChar(char ch) => (Rank)(ch - '1');

        public static bool IsValid(this Rank rank)
            => rank >= Rank.Rank1 && rank <= Rank.Rank8;

        public static char ToChar(this Rank rank)
            => rank.IsValid() ? (char)((int)rank + '1') : throw new ArgumentOutOfRangeException(nameof(rank));
    }
}
