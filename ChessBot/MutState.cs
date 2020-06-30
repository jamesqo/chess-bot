using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    System.ValueTuple<
        ulong,
        int,
        ChessBot.Types.Move
    >
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
            Heuristic = InitHeuristic();
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
            Heuristic = other.Heuristic;
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
        public int Heuristic { get; internal set; }

        // `move` isn't a field, it's just *very* helpful for debugging purposes
        private Snapshot Snapshot(Move move) => (Board, _bbs, ActiveSide, CastlingRights, EnPassantTarget, HalfMoveClock, FullMoveNumber, Hash, Heuristic, move);
        private void Restore(in Snapshot snapshot) =>
            (_board, _bbs, ActiveSide, CastlingRights, EnPassantTarget, HalfMoveClock, FullMoveNumber, Hash, Heuristic, _) = snapshot;

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

        public MoveEnumerator GetPseudoLegalMoves(MoveFlags flags = MoveFlags.Default, Killers killers = default) => new MoveEnumerator(this, flags, killers);

        public bool IsCapture(Move move)
        {
            Debug.Assert(IsMovePseudoLegal(move.Source, move.Destination));

            var destination = move.Destination;
            if (_board[destination].HasPiece) return true;

            var pawnPlacement = ActivePlayer.GetPiecePlacement(PieceKind.Pawn);
            return (pawnPlacement[move.Source] && destination == EnPassantTarget);
        }

        public bool TryApply(Move move, out InvalidMoveReason error)
        {
            var (source, destination) = (move.Source, move.Destination);
            if (!_board[source].HasPiece)
            {
                error = InvalidMoveReason.EmptySource; return false;
            }

            var piece = _board[source].Piece;
            if (piece.Side != ActiveSide)
            {
                error = InvalidMoveReason.MismatchedSourcePiece; return false;
            }
            if (_board[destination].HasPiece && _board[destination].Piece.Side == piece.Side)
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
            var piece = _board[source].Piece;
            bool isEnPassantCapture = (piece.Kind == PieceKind.Pawn && destination == EnPassantTarget);
            bool isCapture = _board[destination].HasPiece || isEnPassantCapture;
            bool isKingsideCastle = (piece.Kind == PieceKind.King && source.File < FileG && destination == source.Right(2)); // todo: add regression tests for bounds check
            bool isQueensideCastle = (piece.Kind == PieceKind.King && source.File > FileB && destination == source.Left(2)); // todo: add regression tests for bounds check
            bool isPawnAdvanceBy2 = (piece.Kind == PieceKind.Pawn && source.Rank == SecondRank(ActiveSide) && destination == source.Up(ForwardStep(ActiveSide) * 2));

            var (oldCastlingRights, oldEnPassantTarget) = (CastlingRights, EnPassantTarget);

            // Before we do anything, push our old state to the history stack

            _history.Push(Snapshot(move));

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

            // Flip the heuristic (as the active side changed)

            Heuristic = -Heuristic;

            // Only now update the active side (to avoid confusion)

            ActiveSide = OpposingSide;
            
            // Make sure we updated these properties correctly

            Debug.Assert(Hash == InitHash());
            Debug.Assert(Heuristic == InitHeuristic());
        }

        private unsafe void ApplyInternal(
            Location source,
            Location destination,
            PieceKind? promotionKind = null,
            bool isEnPassantCapture = false)
        {
            Debug.Assert(source != destination);
            Debug.Assert(_board[source].HasPiece);
            Debug.Assert(!_board[destination].HasPiece || _board[destination].Piece.Side != _board[source].Piece.Side);

            // Store relevant info about the board
            // It's important to do all of this *before* we actually modify the board.
            var piece = _board[source].Piece;
            var kind = piece.Kind;
            var newKind = promotionKind ?? kind;
            var newPiece = new Piece(piece.Side, newKind);
            bool isCapture = _board[destination].HasPiece || isEnPassantCapture;

            Location toClear = default;
            Piece capturedPiece = default;
            if (isCapture)
            {
                toClear = isEnPassantCapture
                    ? (piece.IsWhite ? destination.Down(1) : destination.Up(1))
                    : destination;
                capturedPiece = _board[toClear].Piece;
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
            Hash ^= ZobristKey.ForPsq(piece, source);
            Hash ^= ZobristKey.ForPsq(newPiece, destination);
            // Update heuristic
            Heuristic -= Evaluation.PsqScore(piece, source);
            Heuristic += Evaluation.PsqScore(newPiece, destination);
            if (kind != newKind) // promoted
            {
                Heuristic -= Evaluation.PieceScore(kind);
                Heuristic += Evaluation.PieceScore(newKind);
            }

            if (isCapture)
            {
                // Update board
                if (isEnPassantCapture) _board[toClear] = default;
                // Update piece placement
                _bbs.PiecePlacement[capturedPiece.ToIndex()] &= ~toClear.GetMask();
                // Update occupies
                _bbs.Occupies[(int)OpposingSide] &= ~toClear.GetMask();
                // Update hash
                Hash ^= ZobristKey.ForPsq(capturedPiece, toClear);
                // Update heuristic
                Heuristic += Evaluation.PieceScore(capturedPiece.Kind);
                Heuristic += Evaluation.PsqScore(capturedPiece, toClear);
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
            // should be enough for the average fen string
            var fen = StringBuilderCache.Acquire(70);

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

            return StringBuilderCache.GetStringAndRelease(fen);
        }

        #region Helper methods and properties

        internal bool CanReallyCastleKingside => CanReallyCastle(kingside: true);
        internal bool CanReallyCastleQueenside => CanReallyCastle(kingside: false);

        // this shoulndn't be true of any valid state
        private bool IsOpposingKingAttacked => FindKing(OpposingSide) is Location loc && ActivePlayer.Attacks[loc];

        private ulong InitHash()
        {
            ulong hash = 0;
            foreach (var tile in Board.GetOccupiedTiles())
            {
                hash ^= ZobristKey.ForPsq(tile.Piece, tile.Location);
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

            // todo: this is a serious perf bottleneck, consider unrolling
            for (var bb = Occupied; !bb.IsZero; bb = bb.ClearNext())
            {
                var source = bb.NextLocation();
                var piece = _board[source].Piece;
                var attacks = GetAttackBitboard(piece, source, Occupied);

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

        private int InitHeuristic()
        {
            return Evaluation.Heuristic(this);
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

            bool piecesBetweenKingAndRook = !(GetLocationsBetween(kingSource, rookSource) & Occupied).IsZero;
            bool kingPassesThroughAttackedLocation = !(GetLocationsBetween(kingSource, kingDestination) & OpposingPlayer.Attacks).IsZero;
            return !(piecesBetweenKingAndRook || IsCheck || kingPassesThroughAttackedLocation);
        }

        internal Location? FindKing(Side side)
        {
            var bb = GetPlayer(side).GetPiecePlacement(PieceKind.King);
            Debug.Assert(bb.CountSetBits() <= 1, "Active player has multiple kings");
            return !bb.IsZero ? bb.NextLocation() : (Location?)null;
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

            var sourceTile = _board[source];
            var destinationTile = _board[destination];
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
