using ChessBot.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ChessBot
{
    /// <summary>
    /// Allows us to pass precomputed information about the state that it would be expensive to compute from scratch.
    /// </summary>
    public class Secrets
    {
        // For now, all of these properties are mandatory
        internal Secrets(
            PlayerProperty<PieceBitboards> piecePlacement,
            ulong hash)
        {
            PiecePlacement = piecePlacement;
            Hash = hash;
        }

        internal PlayerProperty<PieceBitboards> PiecePlacement { get; }
        internal ulong Hash { get; }
    }
}
