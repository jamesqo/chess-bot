using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ChessBot
{
    public class PlayerInfo : IEquatable<PlayerInfo>
    {
        private ChessState _state;
        private ImmutableArray<ChessTile> _occupiedTiles;

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

        public BoardLocation InitialKingLocation =>
            Color == PlayerColor.White ? BoardLocation.Parse("e1") : BoardLocation.Parse("e8");
        public BoardLocation InitialKingsideRookLocation =>
            Color == PlayerColor.White ? BoardLocation.Parse("h1") : BoardLocation.Parse("h8");
        public BoardLocation InitialQueensideRookLocation =>
            Color == PlayerColor.White ? BoardLocation.Parse("a1") : BoardLocation.Parse("a8");

        public bool Equals([AllowNull] PlayerInfo other)
        {
            // We ignore _state and the associated fields intentionally
            if (other == null) return false;
            return Color == other.Color
                && HasCastled == other.HasCastled
                && HasMovedKing == other.HasMovedKing
                && HasMovedKingsideRook == other.HasMovedKingsideRook
                && HasMovedQueensideRook == other.HasMovedQueensideRook;
        }

        public override int GetHashCode() => throw new NotImplementedException();

        public ImmutableArray<ChessTile> GetOccupiedTiles()
        {
            if (_occupiedTiles.IsDefault)
            {
                var builder = ImmutableArray.CreateBuilder<ChessTile>();
                foreach (var tile in _state.GetOccupiedTiles())
                {
                    if (tile.Piece.Color == Color)
                    {
                        builder.Add(tile);
                    }
                }
                // todo (perf): we should track of how many pieces were captured so we can use MoveToImmutable
                _occupiedTiles = builder.ToImmutable();
            }
            return _occupiedTiles;
        }

        public PlayerInfo SetColor(PlayerColor value) => new PlayerInfo(this) { Color = value };
        public PlayerInfo SetHasCastled(bool value) => new PlayerInfo(this) { HasCastled = value };
        public PlayerInfo SetHasMovedKing(bool value) => new PlayerInfo(this) { HasMovedKing = value };
        public PlayerInfo SetHasMovedKingsideRook(bool value) => new PlayerInfo(this) { HasMovedKingsideRook = value };
        public PlayerInfo SetHasMovedQueensideRook(bool value) => new PlayerInfo(this) { HasMovedQueensideRook = value };

        internal PlayerInfo SetState(ChessState value) => new PlayerInfo(this) { _state = value };

        public override string ToString()
        {
            var propValues = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(this)}");
            return $"{{{string.Join(Environment.NewLine, propValues)}}}";
        }
    }
}
