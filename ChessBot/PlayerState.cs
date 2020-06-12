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
    public class Player
    {
        internal Player(MutState parent, Side side)
        {
            _parent = parent;
            Side = side;
        }

        private readonly MutState _parent;
        public Side Side { get; }

        /// <summary>
        /// List of locations attacked by this player.
        /// </summary>
        public unsafe Bitboard Attacks => _parent._bbs.Attacks[(int)Side];

        /// <summary>
        /// List of locations occupied by this player.
        /// </summary>
        public unsafe Bitboard Occupies => _parent._bbs.Occupies[(int)Side];

        public bool CanCastleKingside => (_parent.CastlingRights & GetKingsideCastleFlag(Side)) != 0;
        public bool CanCastleQueenside => (_parent.CastlingRights & GetQueensideCastleFlag(Side)) != 0;

        /// <summary>
        /// Gets a list of locations occupied by a certain kind of piece on this player's side.
        /// </summary>
        /// <param name="kind">The kind of the piece.</param>
        public unsafe Bitboard GetPiecePlacement(PieceKind kind)
        {
            if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
            return _parent._bbs.PiecePlacement[new Piece(Side, kind).ToIndex()];
        }

        // this is slow, don't use it on hot codepaths
        public IEnumerable<Tile> GetOccupiedTiles()
        {
            // we don't use `yield return` because the caller could modify the parent in between yields
            var list = new List<Tile>();
            for (var bb = Occupies; !bb.IsZero; bb = bb.ClearNext())
            {
                var location = bb.NextLocation();
                list.Add(new Tile(location, _parent.Board[location]));
            }
            return list;
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
