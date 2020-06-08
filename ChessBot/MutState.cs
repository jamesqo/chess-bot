using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using static ChessBot.StaticInfo;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot
{
    /// <summary>
    /// Immutable class representing the state of the chess board.
    /// </summary>
    public class MutState : IState
    {
        internal unsafe struct Bitboards
        {
            public fixed ulong PiecePlacement[12];
            public fixed ulong Occupies[2];
            public fixed ulong Attacks[2];
        }

        public unsafe MutState(
            in Board board,
            Side activeSide,
            CastlingRights castlingRights,
            Location? enPassantTarget,
            int halfMoveClock,
            int fullMoveNumber)
        {
            White = new Player(this, Side.White);
            Black = new Player(this, Side.Black);

            _board = board;
            ActiveSide = activeSide;
            CastlingRights = castlingRights;
            EnPassantTarget = enPassantTarget;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
            Hash = InitHash();

            InitPiecePlacement();
            InitOccupies();
            InitAttacks();
        }

        private MutState(MutState other)
        {
            White = new Player(this, Side.White);
            Black = new Player(this, Side.Black);

            _board = other.Board;
            ActiveSide = other.ActiveSide;
            CastlingRights = other.CastlingRights;
            EnPassantTarget = other.EnPassantTarget;
            HalfMoveClock = other.HalfMoveClock;
            FullMoveNumber = other.FullMoveNumber;
            Hash = other.Hash;

            _bbs = other._bbs;
        }

        #region Fields

        private Board _board;
        internal Bitboards _bbs;

        public Player White { get; }
        public Player Black { get; }

        public ref readonly Board Board => ref _board;
        public Side ActiveSide { get; internal set; }
        public CastlingRights CastlingRights { get; private set; }
        public Location? EnPassantTarget { get; private set; }
        // todo: take this into account for draws
        public int HalfMoveClock { get; private set; }
        public int FullMoveNumber { get; private set; }
        public ulong Hash { get; internal set; }

        #endregion

        public bool IsCheck => FindKing(ActiveSide) is Location loc && OpposingPlayer.Attacks[loc];

        public Bitboard Occupied
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] // this wasn't being inlined for whatever reason
            get => White.Occupies | Black.Occupies;
        }

        public bool WhiteToMove => ActiveSide.IsWhite();
        public Side OpposingSide => ActiveSide.Flip();
        public Player ActivePlayer => GetPlayer(ActiveSide);
        public Player OpposingPlayer => GetPlayer(OpposingSide);

        public Player GetPlayer(Side side) => side.IsWhite() ? White : Black;

        public bool TryApply(Move move, out InvalidMoveReason error)
        {
            var (source, destination) = (move.Source, move.Destination);
            if (!Board[source].HasPiece)
            {
                error = InvalidMoveReason.EmptySource; return false;
            }

            var piece = Board[source].Piece;
            if (piece.Side != ActiveSide)
            {
                error = InvalidMoveReason.MismatchedSourcePiece; return false;
            }
            if (Board[destination].HasPiece && Board[destination].Piece.Side == piece.Side)
            {
                error = InvalidMoveReason.DestinationOccupiedByFriendlyPiece; return false;
            }

            bool promotes = (piece.Kind == PieceKind.Pawn && destination.Rank == EighthRank(ActiveSide));
            if (move.PromotionKind.HasValue != promotes)
            {
                error = InvalidMoveReason.BadPromotionKind; return false;
            }

            if (!IsMovePossible(source, destination))
            {
                error = InvalidMoveReason.ViolatesMovementRules; return false;
            }

            ApplyUnsafe(move);

            // Ensure our king isn't attacked afterwards

            // todo: as an optimization, we could narrow our search if our king is currently in check.
            // we may only bother for the three types of moves that could possibly get us out of check.

            if (IsOpposingKingAttacked) // this corresponds to the king that was active in the previous state
            {
                Undo(move);
                error = InvalidMoveReason.AllowsKingToBeAttacked; return false;
            }

            error = InvalidMoveReason.None;
            return true;
        }

        // does not perform any validity checks
        private void ApplyUnsafe(Move move)
        {
            var (source, destination) = (move.Source, move.Destination);
            var piece = Board[source].Piece;
            bool isEnPassantCapture = (piece.Kind == PieceKind.Pawn && destination == EnPassantTarget);
            bool isCapture = Board[destination].HasPiece || isEnPassantCapture;
            bool isKingsideCastle = (piece.Kind == PieceKind.King && destination == source.Right(2));
            bool isQueensideCastle = (piece.Kind == PieceKind.King && destination == source.Left(2));
            bool isPawnAdvanceBy2 = (piece.Kind == PieceKind.Pawn && source.Rank == SecondRank(ActiveSide) && destination == source.Up(ForwardStep(ActiveSide) * 2));

            var (oldCastlingRights, oldEnPassantTarget) = (CastlingRights, EnPassantTarget);

            // Update the board

            ApplyInternal(source, destination, move.PromotionKind, isEnPassantCapture);

            // Handle castling specially because we have to move the rook too

            if (isKingsideCastle || isQueensideCastle)
            {
                var rookSource = GetStartLocation(ActiveSide, PieceKind.Rook, isKingsideCastle);
                var rookDestination = isKingsideCastle ? rookSource.Left(2) : rookSource.Right(3);
                ApplyInternal(rookSource, rookDestination);
            }

            // Update castling rights

            switch (piece.Kind)
            {
                case PieceKind.King:
                    CastlingRights &= ~GetCastleFlags(ActiveSide);
                    break;
                case PieceKind.Rook:
                    // todo: we should also update these properties if the rook is captured, as opposed to being moved.
                    if (source == GetStartLocation(ActiveSide, PieceKind.Rook, kingside: true)) CastlingRights &= ~GetKingsideCastleFlag(ActiveSide);
                    if (source == GetStartLocation(ActiveSide, PieceKind.Rook, kingside: false)) CastlingRights &= ~GetQueensideCastleFlag(ActiveSide);
                    break;
            }

            if (isCapture)
            {
                if (destination == GetStartLocation(OpposingSide, PieceKind.Rook, kingside: true))
                {
                    CastlingRights &= ~GetKingsideCastleFlag(OpposingSide);
                }
                else if (destination == GetStartLocation(OpposingSide, PieceKind.Rook, kingside: false))
                {
                    CastlingRights &= ~GetQueensideCastleFlag(OpposingSide);
                }
            }

            // Update en passant target

            EnPassantTarget = isPawnAdvanceBy2 ? source.Up(ForwardStep(ActiveSide)) : (Location?)null;

            // Update halfmove clock and fullmove number

            HalfMoveClock = (isCapture || piece.Kind == PieceKind.Pawn) ? 0 : (HalfMoveClock + 1);
            if (!ActiveSide.IsWhite())
            {
                FullMoveNumber++;
            }

            // Finish updating the hash

            Hash ^= ZobristKey.ForActiveSide(ActiveSide);
            Hash ^= ZobristKey.ForActiveSide(OpposingSide);
            Hash ^= ZobristKey.ForCastlingRights(oldCastlingRights);
            Hash ^= ZobristKey.ForCastlingRights(CastlingRights);
            if (oldEnPassantTarget.HasValue) Hash ^= ZobristKey.ForEnPassantFile(oldEnPassantTarget.Value.File);
            if (EnPassantTarget.HasValue) Hash ^= ZobristKey.ForEnPassantFile(EnPassantTarget.Value.File);

            // Only now update the active side (to avoid confusion)

            ActiveSide = OpposingSide;
        }

        private unsafe void ApplyInternal(
            Location source,
            Location destination,
            PieceKind? promotionKind = null,
            bool isEnPassantCapture = false)
        {
            Debug.Assert(source != destination);
            Debug.Assert(Board[source].HasPiece);
            Debug.Assert(!Board[destination].HasPiece || Board[destination].Piece.Side != Board[source].Piece.Side);

            var piece = Board[source].Piece;
            var newKind = promotionKind ?? piece.Kind;
            var newPiece = new Piece(piece.Side, newKind);
            // Update board
            _board[source] = default;
            _board[destination] = newPiece;
            // Update piece placement
            _bbs.PiecePlacement[piece.ToIndex()] &= ~source.GetMask();
            _bbs.PiecePlacement[newPiece.ToIndex()] |= destination.GetMask();
            // Update occupies
            _bbs.Occupies[(int)ActiveSide] &= ~source.GetMask();
            _bbs.Occupies[(int)ActiveSide] |= destination.GetMask();
            // Update hash
            Hash ^= ZobristKey.ForPieceSquare(piece, source);
            Hash ^= ZobristKey.ForPieceSquare(newPiece, destination);

            bool isCapture = Board[destination].HasPiece || isEnPassantCapture;
            if (isCapture)
            {
                var toClear = isEnPassantCapture
                    ? (piece.IsWhite ? destination.Down(1) : destination.Up(1))
                    : destination;
                var capturedPiece = Board[toClear].Piece;
                // Update board
                if (isEnPassantCapture) _board[toClear] = default;
                // Update piece placement
                _bbs.PiecePlacement[capturedPiece.ToIndex()] &= ~toClear.GetMask();
                // Update occupies
                _bbs.Occupies[(int)OpposingSide] &= ~toClear.GetMask();
                // Update hash
                Hash ^= ZobristKey.ForPieceSquare(capturedPiece, toClear);
            }

            // Recompute attack vectors
            InitAttacks();
        }

        private void Undo(Move move)
        {
            Debug.Assert(IsOpposingKingAttacked); // for now, this is the only scenario in which we call this

            // todo
        }

        public override string ToString()
        {
            var fen = new StringBuilder();

            for (var rank = Rank8; rank >= Rank1; rank--)
            {
                int gap = 0;
                for (var file = FileA; file <= FileH; file++)
                {
                    var piece = Board[(file, rank)];
                    if (!piece.HasPiece) gap++;
                    else
                    {
                        if (gap > 0) fen.Append(gap);
                        fen.Append(piece.Piece.ToDisplayChar());
                        gap = 0;
                    }
                }
                if (gap > 0) fen.Append(gap);
                if (rank > Rank1) fen.Append('/');
            }
            fen.Append(' ');
            fen.Append(WhiteToMove ? 'w' : 'b');
            fen.Append(' ');

            bool any = White.CanCastleKingside || White.CanCastleQueenside || Black.CanCastleKingside || Black.CanCastleQueenside;
            if (!any) fen.Append('-');
            else
            {
                if (White.CanCastleKingside) fen.Append('K');
                if (White.CanCastleQueenside) fen.Append('Q');
                if (Black.CanCastleKingside) fen.Append('k');
                if (Black.CanCastleQueenside) fen.Append('q');
            }
            fen.Append(' ');

            fen.Append(EnPassantTarget?.ToString() ?? "-");
            fen.Append(' ');

            fen.Append(HalfMoveClock);
            fen.Append(' ');

            fen.Append(FullMoveNumber);

            return fen.ToString();
        }

        #region Helper methods and properties

        // this shoulndn't be true of any valid state
        private bool IsOpposingKingAttacked => FindKing(OpposingSide) is Location loc && ActivePlayer.Attacks[loc];

        internal bool CanReallyCastleKingside => CanReallyCastle(kingside: true);
        internal bool CanReallyCastleQueenside => CanReallyCastle(kingside: false);

        private ulong InitHash()
        {
            ulong hash = 0;
            foreach (var tile in Board.GetOccupiedTiles())
            {
                hash ^= ZobristKey.ForPieceSquare(tile.Piece, tile.Location);
            }

            hash ^= ZobristKey.ForActiveSide(ActiveSide);
            hash ^= ZobristKey.ForCastlingRights(CastlingRights);
            if (EnPassantTarget.HasValue) hash ^= ZobristKey.ForEnPassantFile(EnPassantTarget.Value.File);
            return hash;
        }

        private unsafe void InitPiecePlacement()
        {
            foreach (var tile in Board.GetOccupiedTiles())
            {
                var piece = tile.Piece;
                _bbs.PiecePlacement[piece.ToIndex()] |= tile.Location.GetMask();
            }
        }
        
        private unsafe void InitOccupies()
        {
            for (var kind = PieceKind.Pawn; kind <= PieceKind.King; kind++)
            {
                _bbs.Occupies[0] |= White.GetPiecePlacement(kind);
                _bbs.Occupies[1] |= Black.GetPiecePlacement(kind);
            }
        }

        private unsafe void InitAttacks()
        {
            for (var bb = Occupied; bb != Bitboard.Zero; bb = bb.ClearLsb())
            {
                var source = bb.NextLocation();
                var attacks = GetModifiedAttackBitboard(source);

                if (White.Occupies[source])
                {
                    _bbs.Attacks[0] |= attacks;
                }
                else
                {
                    _bbs.Attacks[1] |= attacks;
                }
            }
        }

        private bool CanReallyCastle(bool kingside)
        {
            bool flag = kingside ? ActivePlayer.CanCastleKingside : ActivePlayer.CanCastleQueenside;
            if (!flag) return false;

            // the above flag does not account for situations that temporarily prevent castling, so check for those here
            // note: we don't guard against castling into check because that's taken care of later
            var kingSource = GetStartLocation(ActiveSide, PieceKind.King);
            var rookSource = GetStartLocation(ActiveSide, PieceKind.Rook, kingside);
            var kingDestination = kingside ? kingSource.Right(2) : kingSource.Left(2);

            bool piecesBetweenKingAndRook = (GetLocationsBetween(kingSource, rookSource) & Occupied) != Bitboard.Zero;
            bool kingPassesThroughAttackedLocation = (GetLocationsBetween(kingSource, kingDestination) & OpposingPlayer.Attacks) != Bitboard.Zero;
            return !(piecesBetweenKingAndRook || IsCheck || kingPassesThroughAttackedLocation);
        }

        internal Location? FindKing(Side side)
        {
            var bb = GetPlayer(side).GetPiecePlacement(PieceKind.King);
            Debug.Assert(bb.PopCount() <= 1, $"{side} has more than one king");
            return bb != Bitboard.Zero ? bb.NextLocation() : (Location?)null;
        }

        // todo: now that GetPossibleDestinations returns a bitboard, it shouldn't be that expensive to check for membership.
        /// <summary>
        /// Checks whether it's possible to move the piece on <paramref name="source"/> to <paramref name="destination"/>.
        /// Ignores whether we would create an invalid position by putting our king in check.
        /// <br/>
        /// This is basically equivalent to checking whether GetPossibleDestinations(<paramref name="source"/>) contains <paramref name="destination"/>.
        /// </summary>
        internal bool IsMovePossible(Location source, Location destination, bool allowCastling = true)
        {
            Debug.Assert(Board[source].HasPiece);

            if (source == destination)
            {
                return false;
            }

            var sourceTile = Board[source];
            var destinationTile = Board[destination];
            var piece = sourceTile.Piece;

            if (destinationTile.HasPiece && destinationTile.Piece.Side == piece.Side)
            {
                return false;
            }

            bool canMoveIfUnblocked;
            bool canPieceBeBlocked = false;
            var (deltaX, deltaY) = (destination.File - source.File, destination.Rank - source.Rank);

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    canMoveIfUnblocked = (deltaX.Abs() == deltaY.Abs());
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.King:
                    canMoveIfUnblocked = (deltaX.Abs() <= 1 && deltaY.Abs() <= 1) ||
                        (allowCastling && deltaX == 2 && deltaY == 0 && CanReallyCastleKingside) ||
                        (allowCastling && deltaX == -2 && deltaY == 0 && CanReallyCastleQueenside);
                    break;
                case PieceKind.Knight:
                    canMoveIfUnblocked = (deltaX.Abs() == 1 && deltaY.Abs() == 2) || (deltaX.Abs() == 2 && deltaY.Abs() == 1);
                    break;
                case PieceKind.Pawn:
                    var (forward, secondRank) = (ForwardStep(piece.Side), SecondRank(piece.Side));
                    bool isValidAdvance = (deltaX == 0 && (deltaY == forward || (deltaY == forward * 2 && source.Rank == secondRank)) && !destinationTile.HasPiece);
                    bool isValidCapture = ((deltaX.Abs() == 1 && deltaY == forward) && (destinationTile.HasPiece || destination == EnPassantTarget));

                    canMoveIfUnblocked = (isValidAdvance || isValidCapture);
                    canPieceBeBlocked = isValidAdvance;
                    break;
                case PieceKind.Queen:
                    canMoveIfUnblocked = (deltaX == 0 || deltaY == 0 || deltaX.Abs() == deltaY.Abs());
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.Rook:
                    canMoveIfUnblocked = (deltaX == 0 || deltaY == 0);
                    canPieceBeBlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return canMoveIfUnblocked && (!canPieceBeBlocked || (GetLocationsBetween(source, destination) & Occupied) == Bitboard.Zero);
        }

        // todo: calculating this is a perf bottleneck, so pass it along in Secrets instead
        /// <summary>
        /// Returns a list of locations that are attacked by the piece at <paramref name="source"/>.
        /// </summary>
        internal Bitboard GetModifiedAttackBitboard(Location source)
        {
            Debug.Assert(Board[source].HasPiece);

            var piece = Board[source].Piece;
            var kind = piece.Kind;
            var attacks = GetAttackBitboard(piece, source);

            var totalOccupied = Occupied; // queens, rooks, and bishops can be blocked by pieces of either side
            if (kind == PieceKind.Bishop || kind == PieceKind.Queen)
            {
                attacks = RestrictDiagonally(attacks, totalOccupied, source);
            }
            if (kind == PieceKind.Rook || kind == PieceKind.Queen)
            {
                attacks = RestrictOrthogonally(attacks, totalOccupied, source);
            }
            // It's possible we may have left in squares that are occupied by our own camp. This doesn't affect
            // any of the use cases for this bitboard, though.

            return attacks;
        }

        /// <summary>
        /// Returns the tiles along a vertical, horizontal, or diagonal line between <paramref name="source"/> and <paramref name="destination"/>, exclusive.
        /// </summary>
        private static Bitboard GetLocationsBetween(Location source, Location destination)
        {
            Debug.Assert(source != destination);

            var (deltaX, deltaY) = (destination.File - source.File, destination.Rank - source.Rank);
            var result = Bitboard.Zero;

            if (deltaX == 0)
            {
                // Vertical
                var start = (deltaY > 0) ? source : destination;
                int shift = deltaY.Abs();
                for (int dy = 1; dy < shift; dy++)
                {
                    result |= start.Up(dy).GetMask();
                }
            }
            else if (deltaY == 0)
            {
                // Horizontal
                var start = (deltaX > 0) ? source : destination;
                int shift = deltaX.Abs();
                for (int dx = 1; dx < shift; dx++)
                {
                    result |= start.Right(dx).GetMask();
                }
            }
            else
            {
                // Diagonal
                Debug.Assert(deltaX.Abs() == deltaY.Abs());

                var start = (deltaX > 0) ? source : destination;
                int shift = deltaX.Abs();
                int slope = (deltaX == deltaY) ? 1 : -1;
                for (int dx = 1; dx < shift; dx++)
                {
                    int dy = dx * slope;
                    result |= start.Add(dx, dy).GetMask();
                }
            }

            return result;
        }

        private static Bitboard RestrictDiagonally(Bitboard attacks, Bitboard occupied, Location source)
        {
            Location next;

            for (var prev = source; prev.Rank < Rank8 && prev.File < FileH; prev = next)
            {
                next = prev.Add(1, 1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.Northeast);
                    break;
                }
            }

            for (var prev = source; prev.Rank > Rank1 && prev.File < FileH; prev = next)
            {
                next = prev.Add(1, -1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.Southeast);
                    break;
                }
            }

            for (var prev = source; prev.Rank > Rank1 && prev.File > FileA; prev = next)
            {
                next = prev.Add(-1, -1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.Southwest);
                    break;
                }
            }

            for (var prev = source; prev.Rank < Rank8 && prev.File > FileA; prev = next)
            {
                next = prev.Add(-1, 1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.Northwest);
                    break;
                }
            }

            return attacks;
        }

        private static Bitboard RestrictOrthogonally(Bitboard attacks, Bitboard occupied, Location source)
        {
            Location next;

            for (var prev = source; prev.Rank < Rank8; prev = next)
            {
                next = prev.Up(1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.North);
                    break;
                }
            }

            for (var prev = source; prev.File < FileH; prev = next)
            {
                next = prev.Right(1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.East);
                    break;
                }
            }

            for (var prev = source; prev.Rank > Rank1; prev = next)
            {
                next = prev.Down(1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.South);
                    break;
                }
            }

            for (var prev = source; prev.File > FileA; prev = next)
            {
                next = prev.Left(1);
                if (occupied[next])
                {
                    attacks &= GetStopMask(next, Direction.West);
                    break;
                }
            }

            return attacks;
        }

        internal MutState Clone() => new MutState(this);

        #endregion
    }
}
