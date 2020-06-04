using ChessBot.Types;
using System.Collections.Immutable;

namespace ChessBot
{
    /// <summary>
    /// Allows us to pass precomputed information about the state that it would be expensive to compute from scratch.
    /// </summary>
    public class Secrets
    {
        // For now, all of these properties are mandatory
        internal Secrets(
            PlayerProperty<ImmutableArray<Bitboard>> pieceMasks,
            ulong hash)
        {
            PieceMasks = pieceMasks;
            Hash = hash;
        }

        internal PlayerProperty<ImmutableArray<Bitboard>> PieceMasks { get; }
        internal ulong Hash { get; }
    }
}
