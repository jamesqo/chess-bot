using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ChessBot
{
    public class PlayerInfo : IEquatable<PlayerInfo>
    {
        private State _state;
        private ImmutableArray<Tile> _occupiedTiles;
        private int _pieceCount = -1;

        internal int PieceCount => (_pieceCount >= 0 ? _pieceCount : (_pieceCount = ComputePieceCount()));

        private int ComputePieceCount()
        {
            // todo: enforce _state isn't null

            int count = 0;
            foreach (var tile in _state.GetTiles())
            {
                if (tile.HasPiece && tile.Piece.Color == Color) count++;
            }
            return count;
        }

        public PlayerInfo(
            PlayerColor color,
            bool canCastleKingside = true,
            bool canCastleQueenside = true)
        {
            Color = color;
            CanCastleKingside = canCastleKingside;
            CanCastleQueenside = canCastleQueenside;
        }

        private PlayerInfo(PlayerInfo other) : this(
            other.Color,
            other.CanCastleKingside,
            other.CanCastleQueenside)
        {
            _state = other._state;
            _occupiedTiles = other._occupiedTiles;
            _pieceCount = other._pieceCount;
        }

        public PlayerColor Color { get; private set; }
        public bool CanCastleKingside { get; private set; }
        public bool CanCastleQueenside { get; private set; }

        // todo: avoid use of Parse() here
        public Location InitialKingLocation =>
            Color == PlayerColor.White ? Location.Parse("e1") : Location.Parse("e8");
        public Location InitialKingsideRookLocation =>
            Color == PlayerColor.White ? Location.Parse("h1") : Location.Parse("h8");
        public Location InitialQueensideRookLocation =>
            Color == PlayerColor.White ? Location.Parse("a1") : Location.Parse("a8");

        public bool Equals([AllowNull] PlayerInfo other)
        {
            // We ignore _state and the associated fields intentionally
            if (other == null) return false;
            return Color == other.Color
                && CanCastleKingside == other.CanCastleKingside
                && CanCastleQueenside == other.CanCastleQueenside;
        }

        public override int GetHashCode() => throw new NotImplementedException();

        public ImmutableArray<Tile> GetOccupiedTiles()
        {
            // todo: enforce _state isn't null

            if (_occupiedTiles.IsDefault)
            {
                var builder = ImmutableArray.CreateBuilder<Tile>(PieceCount);
                foreach (var tile in _state.GetTiles())
                {
                    if (tile.HasPiece && tile.Piece.Color == Color)
                    {
                        builder.Add(tile);
                    }
                }
                _occupiedTiles = builder.MoveToImmutable();
            }
            return _occupiedTiles;
        }

        public PlayerInfo SetColor(PlayerColor value) => new PlayerInfo(this) { Color = value };
        public PlayerInfo SetCanCastleKingside(bool value) => new PlayerInfo(this) { CanCastleKingside = value };
        public PlayerInfo SetCanCastleQueenside(bool value) => new PlayerInfo(this) { CanCastleQueenside = value };

        internal PlayerInfo SetState(State value) => new PlayerInfo(this) { _state = value };
        internal PlayerInfo SetOccupiedTiles(ImmutableArray<Tile> value) => new PlayerInfo(this) { _occupiedTiles = value };
        internal PlayerInfo SetPieceCount(int value) => new PlayerInfo(this) { _pieceCount = value };

        public override string ToString()
        {
            var propValues = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(this)}");
            return $"{{{string.Join(", ", propValues)}}}";
        }
    }
}
