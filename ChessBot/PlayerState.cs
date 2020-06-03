using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static ChessBot.StaticInfo;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot
{
    /// <summary>
    /// Describes the state of a player in a chess game.
    /// </summary>
    public class PlayerState
    {
        internal PlayerState(
            State parent,
            Side side,
            ImmutableArray<Bitboard> bitboards,
            bool canCastleKingside = true,
            bool canCastleQueenside = true)
        {
            if (bitboards.Length != 6)
            {
                throw new ArgumentException("Incorrect number of bitboards", nameof(bitboards));
            }

            _parent = parent;
            Side = side;
            Bitboards = bitboards;
            CanCastleKingside = canCastleKingside;
            CanCastleQueenside = canCastleQueenside;

            Board = InitBoard();
            Occupies = InitOccupies();
        }

        private PlayerState(PlayerState other) : this(
            other._parent,
            other.Side,
            other.Bitboards,
            other.CanCastleKingside,
            other.CanCastleQueenside)
        {
        }

        private State _parent;
        private Bitboard? _attacks;

        public Side Side { get; private set; }
        public ImmutableArray<Bitboard> Bitboards { get; private set; }
        public bool CanCastleKingside { get; private set; }
        public bool CanCastleQueenside { get; private set; }

        internal Board Board { get; private set; }
        internal Bitboard Occupies { get; private set; }
        internal Bitboard Attacks => _attacks ?? (Bitboard)(_attacks = InitAttacks());

        // todo: this could also simply write to an array instead of creating a new one
        public Board.OccupiedTilesEnumerator GetOccupiedTiles() => Board.EnumerateOccupiedTiles();

        internal PlayerState SetParent(State value) => new PlayerState(this) { _parent = value };
        internal PlayerState SetBitboards(ImmutableArray<Bitboard> value)
        {
            var result = new PlayerState(this) { Bitboards = value };
            result.Board = result.InitBoard(); // this has to be recomputed
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

        private Board InitBoard()
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
            return new Board(value1, value2, value3, value4);
        }

        private Bitboard InitOccupies()
        {
            var result = new BitboardBuilder();
            foreach (var bb in Bitboards) result.SetRange(bb);
            return result.Bitboard;
        }

        private Bitboard InitAttacks()
        {
            Debug.Assert(_parent != null); // todo: this should be true unconditionally

            var result = new BitboardBuilder();
            for (var kind = PieceKind.Pawn; kind <= PieceKind.King; kind++)
            {
                var piece = new Piece(Side, kind);
                for (var pieceBb = Bitboards[(int)kind]; pieceBb != Bitboard.Zero; pieceBb = pieceBb.ClearLsb())
                {
                    var source = new Location((byte)pieceBb.IndexOfLsb());
                    var attackBb = GetAttackBitboard(piece, source);
                    var occupiedBb = Occupies | _parent.GetPlayer(Side.Flip()).Occupies; // we can be blocked by our own pieces as well as our opponents
                    if (kind == PieceKind.Bishop || kind == PieceKind.Queen)
                    {
                        attackBb = RestrictDiagonally(attackBb, occupiedBb, source);
                    }
                    if (kind == PieceKind.Rook || kind == PieceKind.Queen)
                    {
                        attackBb = RestrictOrthogonally(attackBb, occupiedBb, source);
                    }
                    attackBb &= ~Occupies; // we can't attack squares occupied by our own pieces
                    result.SetRange(attackBb);
                }
            }
            return result.Bitboard;
        }

        private static Bitboard RestrictDiagonally(Bitboard attackBb, Bitboard occupiedBb, Location source)
        {
            Location next;

            for (var prev = source; prev.Rank < Rank8 && prev.File < FileH; prev = next)
            {
                next = prev.Up(1).Right(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.Northeast);
                    break;
                }
            }

            for (var prev = source; prev.Rank > Rank1 && prev.File < FileH; prev = next)
            {
                next = prev.Down(1).Right(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.Southeast);
                    break;
                }
            }

            for (var prev = source; prev.Rank > Rank1 && prev.File > FileA; prev = next)
            {
                next = prev.Down(1).Left(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.Southwest);
                    break;
                }
            }

            for (var prev = source; prev.Rank < Rank8 && prev.File > FileA; prev = next)
            {
                next = prev.Up(1).Left(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.Northwest);
                    break;
                }
            }

            return attackBb;
        }

        private static Bitboard RestrictOrthogonally(Bitboard attackBb, Bitboard occupiedBb, Location source)
        {
            Location next;

            for (var prev = source; prev.Rank < Rank8; prev = next)
            {
                next = prev.Up(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.North);
                    break;
                }
            }

            for (var prev = source; prev.File < FileH; prev = next)
            {
                next = prev.Right(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.North);
                    break;
                }
            }

            for (var prev = source; prev.Rank > Rank1; prev = next)
            {
                next = prev.Down(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.North);
                    break;
                }
            }

            for (var prev = source; prev.File > FileA; prev = next)
            {
                next = prev.Left(1);
                if (occupiedBb[next])
                {
                    attackBb &= GetStopMask(next, Direction.North);
                    break;
                }
            }

            return attackBb;
        }
    }
}
