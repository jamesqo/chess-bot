using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ChessBot
{
    /// <summary>
    /// Describes the state of a player in a chess game.
    /// </summary>
    public class PlayerState : IEquatable<PlayerState>
    {
        private State _state;
        private ImmutableArray<Tile> _occupiedTiles;
        private int _pieceCount = -1;

        internal int PieceCount => (_pieceCount >= 0 ? _pieceCount : (_pieceCount = ComputePieceCount()));

        private int ComputePieceCount()
        {
            Debug.Assert(_state != null);

            int count = 0;
            foreach (var tile in _state.GetTiles())
            {
                if (tile.HasPiece && tile.Piece.Side == Side) count++;
            }
            return count;
        }

        public PlayerState(
            Side side,
            bool canCastleKingside = true,
            bool canCastleQueenside = true)
        {
            Side = side;
            CanCastleKingside = canCastleKingside;
            CanCastleQueenside = canCastleQueenside;
        }

        private PlayerState(PlayerState other) : this(
            other.Side,
            other.CanCastleKingside,
            other.CanCastleQueenside)
        {
            _state = other._state;
            _occupiedTiles = other._occupiedTiles;
            _pieceCount = other._pieceCount;
        }

        public Side Side { get; private set; }
        public bool CanCastleKingside { get; private set; }
        public bool CanCastleQueenside { get; private set; }

        public bool Equals([AllowNull] PlayerState other)
        {
            // We ignore _state and the associated fields intentionally
            if (other == null) return false;
            return Side == other.Side
                && CanCastleKingside == other.CanCastleKingside
                && CanCastleQueenside == other.CanCastleQueenside;
        }

        public override int GetHashCode() => HashCode.Combine(Side, CanCastleKingside, CanCastleQueenside);

        public ImmutableArray<Tile> GetOccupiedTiles()
        {
            if (_state == null)
            {
                throw new InvalidOperationException($"{nameof(_state)} isn't set");
            }

            if (_occupiedTiles.IsDefault)
            {
                var builder = ImmutableArray.CreateBuilder<Tile>(PieceCount);
                foreach (var tile in _state.GetTiles())
                {
                    if (tile.HasPiece && tile.Piece.Side == Side)
                    {
                        builder.Add(tile);
                    }
                }
                _occupiedTiles = builder.MoveToImmutable();
            }
            return _occupiedTiles;
        }

        internal PlayerState SetCanCastleKingside(bool value) => new PlayerState(this) { CanCastleKingside = value };
        internal PlayerState SetCanCastleQueenside(bool value) => new PlayerState(this) { CanCastleQueenside = value };
        internal PlayerState SetState(State value) => new PlayerState(this) { _state = value };
        internal PlayerState SetOccupiedTiles(ImmutableArray<Tile> value) => new PlayerState(this) { _occupiedTiles = value };
        internal PlayerState SetPieceCount(int value) => new PlayerState(this) { _pieceCount = value };

        public override string ToString()
        {
            var propValues = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(this)}");
            return $"{{{string.Join(", ", propValues)}}}";
        }
    }
}
