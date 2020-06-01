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
        private ImmutableArray<Tile> _occupiedTiles;

        public PlayerState(
            Side side,
            ImmutableArray<Bitboard> bitboards,
            bool canCastleKingside = true,
            bool canCastleQueenside = true)
        {
            if (bitboards.Length != 6)
            {
                throw new ArgumentException("Incorrect number of bitboards", nameof(bitboards));
            }

            Side = side;
            Bitboards = bitboards;
            CanCastleKingside = canCastleKingside;
            CanCastleQueenside = canCastleQueenside;
        }

        private PlayerState(PlayerState other) : this(
            other.Side,
            other.Bitboards,
            other.CanCastleKingside,
            other.CanCastleQueenside)
        {
            _occupiedTiles = other._occupiedTiles;
        }

        public Side Side { get; private set; }
        public ImmutableArray<Bitboard> Bitboards { get; private set; }
        public bool CanCastleKingside { get; private set; }
        public bool CanCastleQueenside { get; private set; }

        public bool Equals([AllowNull] PlayerState other)
        {
            if (other == null) return false;
            return Side == other.Side
                && Bitboards.SequenceEqual(other.Bitboards)
                && CanCastleKingside == other.CanCastleKingside
                && CanCastleQueenside == other.CanCastleQueenside;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.Add(Side);
            foreach (var bb in Bitboards)
            {
                hc.Add(bb);
            }
            hc.Add(CanCastleKingside);
            hc.Add(CanCastleQueenside);
            return hc.ToHashCode();
        }

        // todo: this could also simply write to an array instead of creating a new one
        public ImmutableArray<Tile> GetOccupiedTiles()
        {
            if (_occupiedTiles.IsDefault)
            {
                var builder = ImmutableArray.CreateBuilder<Tile>(GetPieceCount());
                GetOccupiedTiles(builder);
                _occupiedTiles = builder.MoveToImmutable();
            }
            return _occupiedTiles;
        }

        internal void GetOccupiedTiles(ImmutableArray<Tile>.Builder builder)
        {
            for (int i = 0; i < Bitboards.Length; i++)
            {
                var bb = Bitboards[i];
                var kind = (PieceKind)i;
                Debug.Assert(kind.IsValid());

                var piece = new Piece(Side, kind);
                while (bb != Bitboard.Zero)
                {
                    var location = new Location((byte)bb.IndexOfLsb());
                    var tile = new Tile(location, piece);
                    builder.Add(tile);
                    bb = bb.ClearLsb();
                }
            }
        }

        internal PlayerState SetBitboards(ImmutableArray<Bitboard> value) => new PlayerState(this) { Bitboards = value };
        internal PlayerState SetCanCastleKingside(bool value) => new PlayerState(this) { CanCastleKingside = value };
        internal PlayerState SetCanCastleQueenside(bool value) => new PlayerState(this) { CanCastleQueenside = value };
        internal PlayerState SetOccupiedTiles(ImmutableArray<Tile> value) => new PlayerState(this) { _occupiedTiles = value };

        public override string ToString()
        {
            var propValues = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(prop => $"{prop.Name}: {prop.GetValue(this)}");
            return $"{{{string.Join(", ", propValues)}}}";
        }

        internal int GetPieceCount()
        {
            int count = 0;
            foreach (var bb in Bitboards) count += bb.PopCount();
            return count;
        }
    }
}
