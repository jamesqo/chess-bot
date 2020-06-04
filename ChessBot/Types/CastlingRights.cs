using System;

namespace ChessBot.Types
{
    /// <summary>
    /// Represents the castling availability of both players.
    /// Unaffected by situations that temporarily prevent castling, i.e. if there is a piece between the king and the rook.
    /// </summary>
    [Flags]
    public enum CastlingRights
    {
        K = 1,
        Q = K << 1,
        k = Q << 1,
        q = k << 1,

        None = 0,
        All = K | Q | k | q
    }

    public static class CastlingRightsHelpers
    {
        public static bool IsValid(this CastlingRights castlingRights)
            => castlingRights >= CastlingRights.None && castlingRights <= CastlingRights.All;
    }
}
