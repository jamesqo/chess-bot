using System.Collections.Generic;
using System.Linq;

namespace ChessBot
{
    public class PlayerInfo
    {
        private ChessState _state;

        internal PlayerInfo(
            ChessState state,
            PlayerColor color,
            bool hasCastled = false,
            bool hasMovedKing = false,
            bool hasMovedKingsideRook = false,
            bool hasMovedQueensideRook = false)
        {
            _state = state;
            Color = color;
            HasCastled = hasCastled;
            HasMovedKing = hasMovedKing;
            HasMovedKingsideRook = hasMovedKingsideRook;
            HasMovedQueensideRook = hasMovedQueensideRook;
        }

        public PlayerColor Color { get; }
        public bool HasCastled { get; }
        public bool HasMovedKing { get; }
        public bool HasMovedKingsideRook { get; }
        public bool HasMovedQueensideRook { get; }

        internal bool EqualsIgnoreState(PlayerInfo other)
        {
            return Color == other.Color
                && HasCastled == other.HasCastled
                && HasMovedKing == other.HasMovedKing
                && HasMovedKingsideRook == other.HasMovedKingsideRook
                && HasMovedQueensideRook == other.HasMovedQueensideRook;
        }

        public IEnumerable<ChessTile> GetOccupiedTiles()
            => _state.GetTiles().Where(t => t.HasPiece && t.Piece.Color == Color);

        internal PlayerInfo WithState(ChessState state)
        {
            var clone = (PlayerInfo)MemberwiseClone();
            clone._state = state;
            return clone;
        }
    }
}
