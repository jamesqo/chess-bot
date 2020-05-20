using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ChessBot
{
    public class PlayerInfo : IEquatable<PlayerInfo>
    {
        private ChessState _state;

        public PlayerInfo(
            PlayerColor color,
            bool hasCastled = false,
            bool hasMovedKing = false,
            bool hasMovedKingsideRook = false,
            bool hasMovedQueensideRook = false)
        {
            Color = color;
            HasCastled = hasCastled;
            HasMovedKing = hasMovedKing;
            HasMovedKingsideRook = hasMovedKingsideRook;
            HasMovedQueensideRook = hasMovedQueensideRook;
        }

        private PlayerInfo(PlayerInfo other) : this(
            other.Color,
            other.HasCastled,
            other.HasMovedKing,
            other.HasMovedKingsideRook,
            other.HasMovedQueensideRook)
        {
            _state = other._state;
        }

        public PlayerColor Color { get; private set; }
        public bool HasCastled { get; private set; }
        public bool HasMovedKing { get; private set; }
        public bool HasMovedKingsideRook { get; private set; }
        public bool HasMovedQueensideRook { get; private set; }

        public BoardLocation InitialKingsideRookLocation =>
            Color == PlayerColor.White ? BoardLocation.Parse("h1") : BoardLocation.Parse("h8");
        public BoardLocation InitialQueensideRookLocation =>
            Color == PlayerColor.White ? BoardLocation.Parse("a1") : BoardLocation.Parse("a8");

        public bool Equals([AllowNull] PlayerInfo other)
        {
            if (other == null) return false;
            return Color == other.Color
                && HasCastled == other.HasCastled
                && HasMovedKing == other.HasMovedKing
                && HasMovedKingsideRook == other.HasMovedKingsideRook
                && HasMovedQueensideRook == other.HasMovedQueensideRook;
        }

        public override int GetHashCode() => throw new NotImplementedException();

        public IEnumerable<ChessTile> GetOccupiedTiles()
            => _state.GetOccupiedTiles().Where(t => t.Piece.Color == Color);

        public PlayerInfo SetColor(PlayerColor value) => new PlayerInfo(this) { Color = value };
        public PlayerInfo SetHasCastled(bool value) => new PlayerInfo(this) { HasCastled = value };
        public PlayerInfo SetHasMovedKing(bool value) => new PlayerInfo(this) { HasMovedKing = value };
        public PlayerInfo SetHasMovedKingsideRook(bool value) => new PlayerInfo(this) { HasMovedKingsideRook = value };
        public PlayerInfo SetHasMovedQueensideRook(bool value) => new PlayerInfo(this) { HasMovedQueensideRook = value };

        internal PlayerInfo SetState(ChessState value) => new PlayerInfo(this) { _state = value };

        public override string ToString()
        {
            var propStrings = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(this)}");
            return "{" + string.Join(Environment.NewLine, propStrings) + "}";
        }
    }
}
