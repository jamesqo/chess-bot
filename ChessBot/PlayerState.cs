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
            Tiles = InitTiles();
        }

        private PlayerState(PlayerState other) : this(
            other.Side,
            other.Bitboards,
            other.CanCastleKingside,
            other.CanCastleQueenside)
        {
        }

        public Side Side { get; private set; }
        public ImmutableArray<Bitboard> Bitboards { get; private set; }
        public bool CanCastleKingside { get; private set; }
        public bool CanCastleQueenside { get; private set; }

        internal TileList Tiles { get; private set; }

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
        public OccupiedTilesEnumerator GetOccupiedTiles() => new OccupiedTilesEnumerator(Tiles);

        internal PlayerState SetBitboards(ImmutableArray<Bitboard> value)
        {
            var result = new PlayerState(this) { Bitboards = value };
            result.Tiles = result.InitTiles(); // this has to be recomputed
            return result;
        }
        internal PlayerState SetCanCastleKingside(bool value) => new PlayerState(this) { CanCastleKingside = value };
        internal PlayerState SetCanCastleQueenside(bool value) => new PlayerState(this) { CanCastleQueenside = value };

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

        private TileList InitTiles()
        {
            ulong value1 = 0, value2 = 0, value3 = 0, value4 = 0;
            for (var kind = PieceKind.Pawn; kind <= PieceKind.King; kind++)
            {
                var piece = new Piece(Side, kind);
                int pieceValue = piece.Value + 1; // 0 represents an empty tile
                var bb = Bitboards[(int)kind];

                if ((pieceValue & 1) != 0) value1 |= bb;
                if ((pieceValue & 2) != 0) value2 |= bb;
                if ((pieceValue & 4) != 0) value3 |= bb;
                if ((pieceValue & 8) != 0) value4 |= bb;
            }
            return new TileList(value1, value2, value3, value4);
        }
    }
}
