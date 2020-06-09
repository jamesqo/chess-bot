using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using static ChessBot.StaticInfo;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;
using Snapshot = System.ValueTuple<
    ChessBot.Types.Board,
    ChessBot.MutState.Bitboards,
    ChessBot.Types.Side,
    ChessBot.Types.CastlingRights,
    ChessBot.Types.Location?,
    int,
    int,
    System.ValueTuple<ulong>
>;

namespace ChessBot
{
    /// <summary>
    /// Mutable class representing the state of a chess game.
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
            _history = new Stack<Snapshot>();
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
            _history = Copy(other._history);
        }

        #region Fields

        private Board _board;
        internal Bitboards _bbs;
        private readonly Stack<Snapshot> _history;

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

        private Snapshot Snapshot() => (Board, _bbs, ActiveSide, CastlingRights, EnPassantTarget, HalfMoveClock, FullMoveNumber, Hash);
        private void Restore(in Snapshot snapshot) =>
            (_board, _bbs, ActiveSide, CastlingRights, EnPassantTarget, HalfMoveClock, FullMoveNumber, Hash) = snapshot;

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

        public IEnumerable<Move> GetPseudoLegalMoves()
        {
            // because this method is lazy (ie. uses iterators) and our state is mutable, we have to make a copy of all of the relevant state variables
            return GetPseudoLegalMoves(
                Board,
                ActiveSide,
                EnPassantTarget,
                ActivePlayer.Occupies,
                OpposingPlayer.Occupies,
                CanReallyCastleKingside,
                CanReallyCastleQueenside);
        }

        private static IEnumerable<Move> GetPseudoLegalMoves(
            Board board,
            Side activeSide,
            Location? enPassantTarget,
            Bitboard activeOccupies,
            Bitboard opposingOccupies,
            bool canReallyCastleKingside,
            bool canReallyCastleQueenside)
        {
            Bitboard occupied = activeOccupies | opposingOccupies;

            /// <summary>
            /// Returns a list of locations that the piece at <paramref name="source"/> may move to.
            /// Does not account for whether the move would be invalid because its king is currently in check.
            /// </summary>
            Bitboard GetPseudoLegalDestinations(Location source)
            {
                Debug.Assert(board[source].HasPiece);
                Debug.Assert(board[source].Piece.Side == activeSide);

                var piece = board[source].Piece;
                var result = GetModifiedAttackBitboard(source, piece, occupied);

                var side = piece.Side;
                Debug.Assert(side == activeSide); // this assumption is only used when we check for castling availability

                result &= ~activeOccupies; // we can't move to squares occupied by our own pieces

                switch (piece.Kind)
                {
                    case PieceKind.Pawn:
                        // a pawn can only move to the left/right if it captures an opposing piece
                        var captureMask = opposingOccupies;
                        if (enPassantTarget.HasValue) captureMask |= enPassantTarget.Value.GetMask();
                        result &= captureMask;

                        // the attack vectors also don't include moving forward by 1/2, so OR those in
                        Debug.Assert(source.Rank != EighthRank(side)); // pawns should be promoted once they reach the eighth rank
                        var forward = ForwardStep(side);
                        var up1 = source.Up(forward);
                        if (!occupied[up1])
                        {
                            result |= up1.GetMask();
                            if (source.Rank == SecondRank(side))
                            {
                                var up2 = source.Up(forward * 2);
                                if (!occupied[up2]) result |= up2.GetMask();
                            }
                        }
                        break;
                    case PieceKind.King:
                        if (canReallyCastleKingside) result |= source.Right(2).GetMask();
                        if (canReallyCastleQueenside) result |= source.Left(2).GetMask();
                        break;
                }

                return result;
            }

            for (var ss = activeOccupies; ss != Bitboard.Zero; ss = ss.ClearLsb())
            {
                var source = ss.NextLocation();
                var piece = board[source].Piece;

                for (var ds = GetPseudoLegalDestinations(source); ds != Bitboard.Zero; ds = ds.ClearLsb())
                {
                    var destination = ds.NextLocation();
                    // If we're a pawn moving to the back rank and promoting, there are multiple moves to consider
                    if (piece.Kind == PieceKind.Pawn && source.Rank == SeventhRank(activeSide))
                    {
                        yield return new Move(source, destination, promotionKind: PieceKind.Knight);
                        yield return new Move(source, destination, promotionKind: PieceKind.Bishop);
                        yield return new Move(source, destination, promotionKind: PieceKind.Rook);
                        yield return new Move(source, destination, promotionKind: PieceKind.Queen);
                    }
                    else
                    {
                        yield return new Move(source, destination);
                    }
                }
            }
        }

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

            if (!IsMovePseudoLegal(source, destination))
            {
                error = InvalidMoveReason.ViolatesMovementRules; return false;
            }

            ApplyUnsafe(move);

            // Ensure our king isn't attacked afterwards

            // todo: as an optimization, we could narrow our search if our king is currently in check.
            // we may only bother for the three types of moves that could possibly get us out of check.

            if (IsOpposingKingAttacked) // this corresponds to the king that was active in the previous state
            {
                Undo();
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

            // Before we do anything, push our old state to the history stack

            _history.Push(Snapshot());

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

            // Store relevant info about the board
            // It's important to do all of this *before* we actually modify the board.
            var piece = Board[source].Piece;
            var newKind = promotionKind ?? piece.Kind;
            var newPiece = new Piece(piece.Side, newKind);
            bool isCapture = Board[destination].HasPiece || isEnPassantCapture;

            Location toClear = default;
            Piece capturedPiece = default;
            if (isCapture)
            {
                toClear = isEnPassantCapture
                    ? (piece.IsWhite ? destination.Down(1) : destination.Up(1))
                    : destination;
                capturedPiece = Board[toClear].Piece;
            }

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

            if (isCapture)
            {
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

        public void Undo()
        {
            if (_history.Count == 0)
            {
                throw new InvalidOperationException("Empty history stack");
            }

            Restore(_history.Pop());
        }

        public override string ToString()
        {
            var fen = new StringBuilder();

            Board.ToString(fen);
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

        private bool CanReallyCastleKingside => CanReallyCastle(kingside: true);
        private bool CanReallyCastleQueenside => CanReallyCastle(kingside: false);

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
            for (int i = 0; i < Piece.NumberOfValues; i++) _bbs.PiecePlacement[i] = 0;

            foreach (var tile in Board.GetOccupiedTiles())
            {
                var piece = tile.Piece;
                _bbs.PiecePlacement[piece.ToIndex()] |= tile.Location.GetMask();
            }
        }
        
        private unsafe void InitOccupies()
        {
            _bbs.Occupies[0] = _bbs.Occupies[1] = 0;

            for (var kind = PieceKind.Pawn; kind <= PieceKind.King; kind++)
            {
                _bbs.Occupies[0] |= White.GetPiecePlacement(kind);
                _bbs.Occupies[1] |= Black.GetPiecePlacement(kind);
            }
        }

        private unsafe void InitAttacks()
        {
            _bbs.Attacks[0] = _bbs.Attacks[1] = 0;

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

        // todo: now that GetPseudoLegalDestinations returns a bitboard, it shouldn't be that expensive to check for membership.
        /// <summary>
        /// Checks whether it's possible to move the piece on <paramref name="source"/> to <paramref name="destination"/> according to movement rules.
        /// Ignores whether we would create an invalid position by putting our king in check.
        /// <br/>
        /// This is basically equivalent to checking whether GetPseudoLegalDestinations(<paramref name="source"/>) contains <paramref name="destination"/>.
        /// </summary>
        internal bool IsMovePseudoLegal(Location source, Location destination, bool allowCastling = true)
        {
            Debug.Assert(Board[source].HasPiece);
            Debug.Assert(Board[source].Piece.Side == ActiveSide);

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

        private Bitboard GetModifiedAttackBitboard(Location source)
        {
            Debug.Assert(Board[source].HasPiece);
            return GetModifiedAttackBitboard(source, Board[source].Piece, Occupied);
        }

        // todo: calculating this is a perf bottleneck
        /// <summary>
        /// Returns a list of locations that are attacked by the piece at <paramref name="source"/>.
        /// </summary>
        private static Bitboard GetModifiedAttackBitboard(Location source, Piece piece, Bitboard occupied)
        {
            var kind = piece.Kind;
            var attacks = GetAttackBitboard(piece, source);

            if (kind == PieceKind.Bishop || kind == PieceKind.Queen)
            {
                attacks = RestrictDiagonally(attacks, occupied, source);
            }
            if (kind == PieceKind.Rook || kind == PieceKind.Queen)
            {
                attacks = RestrictOrthogonally(attacks, occupied, source);
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

        internal MutState Copy() => new MutState(this);

        private Stack<T> Copy<T>(Stack<T> source)
        {
            var array = source.ToArray();
            Array.Reverse(array);
            return new Stack<T>(array);
        }

        #endregion
    }
}
