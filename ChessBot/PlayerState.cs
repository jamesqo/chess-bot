using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static ChessBot.StaticInfo;

namespace ChessBot
{
    /// <summary>
    /// Describes the state of a player in a chess game.
    /// </summary>
    public class PlayerState
    {
        internal PlayerState(State parent, Side side)
        {
            _parent = parent;
            Side = side;
        }

        private PlayerState(PlayerState other) : this(
            other._parent,
            other.Side)
        {
        }

        private readonly State _parent;
        public Side Side { get; }

        public Bitboard Attacks => _parent.Attacks.Get(Side);
        public Bitboard Occupies => _parent.Occupies.Get(Side);
        public bool CanCastleKingside => (_parent.CastlingRights & GetKingsideCastleFlag(Side)) != 0;
        public bool CanCastleQueenside => (_parent.CastlingRights & GetQueensideCastleFlag(Side)) != 0;

        public Bitboard GetPieceMask(PieceKind kind)
        {
            if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
            return _parent.PieceMasks.Get(Side)[(int)kind];
        }

        // perf todo
        public IEnumerable<Tile> GetOccupiedTiles()
        {
            foreach (var tile in _parent.GetTiles())
            {
                if (tile.HasPiece && tile.Piece.Side == Side)
                {
                    yield return tile;
                }
            }
        }

        public override string ToString()
        {
            var propValues = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(this)}");
            return $"{{{string.Join(", ", propValues)}}}";
        }
    }
}
