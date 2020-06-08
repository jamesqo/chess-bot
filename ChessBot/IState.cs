using ChessBot.Types;

namespace ChessBot
{
    public interface IState
    {
        ref readonly Board Board { get; }
        Side ActiveSide { get; }
        CastlingRights CastlingRights { get; }
        Location? EnPassantTarget { get; }
        int HalfMoveClock { get; }
        int FullMoveNumber { get; }

        ulong Hash { get; }
        bool IsCheck { get; }
        Bitboard Occupied { get; }

        Player White { get; }
        Player Black { get; }

        // these can all be implemented in terms of the other properties
        bool WhiteToMove { get; }
        Side OpposingSide { get; }
        Player ActivePlayer { get; }
        Player OpposingPlayer { get; }

        Player GetPlayer(Side side);
    }
}
