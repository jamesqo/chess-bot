using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static ChessBot.StaticInfo;

namespace ChessBot
{
    // todo: make this a class again but lazily initialize it
    /// <summary>
    /// Describes the state of a player in a chess game.
    /// </summary>
    public readonly struct PlayerState
    {
        internal PlayerState(State parent, Side side)
        {
            _parent = parent;
            Side = side;
        }

        private readonly State _parent;
        public Side Side { get; }

        /// <summary>
        /// List of locations attacked by this player.
        /// </summary>
        public Bitboard Attacks => _parent.Attacks.Get(Side);

        /// <summary>
        /// List of locations occupied by this player.
        /// </summary>
        public Bitboard Occupies => _parent.Occupies.Get(Side);

        public bool CanCastleKingside => (_parent.CastlingRights & GetKingsideCastleFlag(Side)) != 0;
        public bool CanCastleQueenside => (_parent.CastlingRights & GetQueensideCastleFlag(Side)) != 0;

        /// <summary>
        /// Gets a list of locations occupied by a certain kind of piece on this player's side.
        /// </summary>
        /// <param name="kind">The kind of the piece.</param>
        public Bitboard GetPiecePlacement(PieceKind kind)
        {
            if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
            return _parent.PiecePlacement.Get(Side)[kind];
        }

        // don't use this on hot codepaths
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
            var self = this; // CS1673
            var propValues = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(self)}");
            return $"{{{string.Join(", ", propValues)}}}";
        }
    }
}
