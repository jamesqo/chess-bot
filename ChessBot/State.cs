using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using static ChessBot.StaticInfo;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;
using Pms = ChessBot.PieceMasks;

namespace ChessBot
{
    /// <summary>
    /// Immutable class representing the state of the chess board.
    /// </summary>
    public class State : IEquatable<State>
    {
        public static string StartFen { get; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        public static State Start { get; } = ParseFen(StartFen);

        public State(
            Board board,
            Side activeSide,
            CastlingRights castlingRights,
            Location? enPassantTarget,
            int halfMoveClock,
            int fullMoveNumber,
            Secrets secrets = null)
        {
            Board = board;
            ActiveSide = activeSide;
            CastlingRights = castlingRights;
            EnPassantTarget = enPassantTarget;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
            Hash = secrets?.Hash ?? InitZobristHash();

            PieceMasks = secrets?.PieceMasks ?? InitPieceMasks(board);
            Occupies = InitOccupies();
            Attacks = InitAttacks();

            White = new PlayerState(this, Side.White);
            Black = new PlayerState(this, Side.Black);
        }

        // todo: remove this
        private State(State other) : this(
            other.Board,
            other.ActiveSide,
            other.CastlingRights,
            other.EnPassantTarget,
            other.HalfMoveClock,
            other.FullMoveNumber)
        {
        }

        /// <summary>
        /// Creates a <see cref="State"/> from FEN notation.
        /// </summary>
        public static State ParseFen(string fen)
        {
            var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6) throw new InvalidFenException("Incorrect number of fields");

            var piecePlacement = parts[0];
            var activeSide = parts[1] switch
            {
                "w" => Side.White,
                "b" => Side.Black,
                _ => throw new InvalidFenException($"Invalid active color: {parts[1]}")
            };
            var castlingFlags = parts[2];
            var enPassantTarget = parts[3] switch
            {
                "-" => (Location?)null,
                _ => Location.TryParse(parts[3]) ?? throw new InvalidFenException($"Invalid en passant target: {parts[3]}")
            };
            if (!int.TryParse(parts[4], out var halfMoveClock) || halfMoveClock < 0) throw new InvalidFenException($"Invalid halfmove clock: {parts[4]}");
            if (!int.TryParse(parts[5], out var fullMoveNumber) || fullMoveNumber <= 0) throw new InvalidFenException($"Invalid fullmove number: {parts[5]}");

            // Parse the board
            var board = Board.CreateBuilder();

            var rankDescs = piecePlacement.Split('/');
            if (rankDescs.Length != 8) throw new InvalidFenException("Incorrect number of ranks");

            for (var rank = Rank1; rank <= Rank8; rank++)
            {
                string rankDesc = rankDescs[7 - (int)rank];

                var file = FileA;
                bool allowDigit = true;
                foreach (char ch in rankDesc)
                {
                    var location = new Location(file, rank);
                    if ((ch >= '1' && ch <= '8') && allowDigit)
                    {
                        int skip = (ch - '0');
                        if ((int)file + skip > 8) throw new InvalidFenException("Incorrect number of files");

                        file += skip;
                        allowDigit = false;
                    }
                    else
                    {
                        if ((int)file == 8) throw new InvalidFenException("Incorrect number of files");

                        var side = (char.ToLowerInvariant(ch) == ch) ? Side.Black : Side.White;
                        var kind = char.ToLowerInvariant(ch) switch
                        {
                            'p' => PieceKind.Pawn,
                            'n' => PieceKind.Knight,
                            'b' => PieceKind.Bishop,
                            'r' => PieceKind.Rook,
                            'q' => PieceKind.Queen,
                            'k' => PieceKind.King,
                            _ => throw new InvalidFenException($"Invalid piece kind: {ch}")
                        };
                        var piece = new Piece(side, kind);
                        board[location] = piece;
                        file++;
                        allowDigit = true;
                    }
                }

                if ((int)file != 8) throw new InvalidFenException("Incorrect number of files");
            }

            var castlingRights = CastlingRights.None;
            if (castlingFlags != "-")
            {
                foreach (char ch in castlingFlags)
                {
                    switch (ch)
                    {
                        case 'K': castlingRights |= CastlingRights.K; break;
                        case 'Q': castlingRights |= CastlingRights.Q; break;
                        case 'k': castlingRights |= CastlingRights.k; break;
                        case 'q': castlingRights |= CastlingRights.q; break;
                        default: throw new InvalidFenException($"Invalid castling flag: {ch}");
                    }
                }
            }

            return new State(
                board: board.Value,
                activeSide: activeSide,
                castlingRights: castlingRights,
                enPassantTarget: enPassantTarget,
                halfMoveClock: halfMoveClock,
                fullMoveNumber: fullMoveNumber);
        }

        // cached properties
        private bool? _isCheck;
        private bool? _canCastleKingside;
        private bool? _canCastleQueenside;

        /// <summary>
        /// The next side to move.
        /// </summary>
        public Side ActiveSide { get; private set; }

        /// <summary>
        /// The castling rights for both players.
        /// </summary>
        public CastlingRights CastlingRights { get; }

        /// <summary>
        /// The destination square of an en passant capture if a pawn made a two-square move during the last turn, otherwise <see langword="null"/>.
        /// </summary>
        public Location? EnPassantTarget { get; }

        // todo: take this into account for draws
        /// <summary>
        /// The number of halfmoves since the last capture or pawn advance.
        /// Used to determine if a draw can be claimed under the 50-move rule.
        /// </summary>
        public int HalfMoveClock { get; }

        /// <summary>
        /// The number of fullmoves since the start of the game.
        /// </summary>
        public int FullMoveNumber { get; }

        /// <summary>
        /// The Zobrist hash value for this state.
        /// </summary>
        public ulong Hash { get; private set; }

        public PlayerState White { get; }
        public PlayerState Black { get; }

        internal Board Board { get; }
        internal PlayerProperty<PieceMasks> PieceMasks { get; }
        internal PlayerProperty<Bitboard> Occupies { get; }
        internal PlayerProperty<Bitboard> Attacks { get; }

        public PlayerState ActivePlayer => GetPlayer(ActiveSide);
        public Side OpposingSide => ActiveSide.Flip();
        public PlayerState OpposingPlayer => GetPlayer(OpposingSide);

        public bool IsCheck => _isCheck ?? (bool)(_isCheck = (FindKing(ActiveSide) is Location loc && IsAttackedBy(OpposingSide, loc)));
        // these properties are very expensive to compute, so we're omitting them for now
        //public bool IsCheckmate => IsCheck && IsTerminal;
        //public bool IsStalemate => !IsCheck && IsTerminal;
        //public bool IsTerminal => _isTerminal ?? (bool)(_isTerminal = !GetMoves().Any());
        public bool WhiteToMove => ActiveSide.IsWhite();

        public Bitboard Occupied
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] // this wasn't being inlined for whatever reason
            get => Occupies.White | Occupies.Black;
        }

        // don't use these apis on hot codepaths-- instead, use Board[location].
        public Tile this[Location location] => new Tile(location, Board[location]);
        public Tile this[File file, Rank rank] => this[(file, rank)];
        public Tile this[string location] => this[Location.Parse(location)];

        public State Apply(string move) => Apply(Move.Parse(move, this));
        public State Apply(Move move) => TryApply(move, out var error) ?? throw error;

        public State? TryApply(string move, out InvalidMoveException error)
        {
            Move moveObj;
            try
            {
                moveObj = Move.Parse(move, this);
            }
            catch (InvalidMoveException e)
            {
                error = e;
                return null;
            }
            return TryApply(moveObj, out error);
        }

        public State? TryApply(Move move, out InvalidMoveException error)
        {
            var (source, destination) = (move.Source, move.Destination);
            if (!Board[source].HasPiece)
            {
                error = new InvalidMoveException("Source tile is empty"); return null;
            }

            var piece = Board[source].Piece;
            if (piece.Side != ActiveSide)
            {
                error = new InvalidMoveException("Piece's color does not match active player's color"); return null;
            }
            if (Board[destination].HasPiece && Board[destination].Piece.Side == piece.Side)
            {
                error = new InvalidMoveException("Destination tile is already occupied by a piece of the same color"); return null;
            }

            bool promotes = (piece.Kind == PieceKind.Pawn && destination.Rank == EighthRank(ActiveSide));
            if (move.PromotionKind.HasValue != promotes)
            {
                error = new InvalidMoveException("A promotion happens iff a pawn moves to the back rank"); return null;
            }

            if (!IsMovePossible(source, destination))
            {
                error = new InvalidMoveException($"Movement rules do not allow {piece} to be brought from {source} to {destination}"); return null;
            }

            var result = ApplyUnsafe(move);

            // Ensure our king isn't attacked afterwards

            // todo: as an optimization, we could narrow our search if our king is currently in check.
            // we may only bother for the three types of moves that could possibly get us out of check.

            if (result.CanAttackOpposingKing) // this corresponds to the king that was active in the previous state
            {
                error = new InvalidMoveException($"Move is invalid since it lets {ActiveSide}'s king be attacked"); return null;
            }

            error = null;
            return result;
        }

        private State ApplyUnsafe(Move move)
        {
            var (source, destination) = (move.Source, move.Destination);
            var piece = Board[source].Piece;
            bool isEnPassantCapture = (piece.Kind == PieceKind.Pawn && destination == EnPassantTarget);
            bool isCapture = Board[destination].HasPiece || isEnPassantCapture;
            bool isKingsideCastle = (piece.Kind == PieceKind.King && destination == source.Right(2));
            bool isQueensideCastle = (piece.Kind == PieceKind.King && destination == source.Left(2));

            var (newBoard, newPieceMasks, newCastlingRights, newHash) = (Board, PieceMasks, CastlingRights, Hash);

            // Update the board

            ApplyInternal(ref newBoard, ref newPieceMasks, ref newHash, source, destination, move.PromotionKind, isEnPassantCapture);

            // Handle castling specially because we have to move the rook too

            if (isKingsideCastle || isQueensideCastle)
            {
                var rookSource = GetStartLocation(ActiveSide, PieceKind.Rook, isKingsideCastle);
                var rookDestination = isKingsideCastle ? rookSource.Left(2) : rookSource.Right(3);
                ApplyInternal(ref newBoard, ref newPieceMasks, ref newHash, rookSource, rookDestination);
            }

            // Update castling rights

            switch (piece.Kind)
            {
                case PieceKind.King:
                    newCastlingRights &= ~GetCastleFlags(ActiveSide);
                    break;
                case PieceKind.Rook:
                    // todo: we should also update these properties if the rook is captured, as opposed to being moved.
                    if (source == GetStartLocation(ActiveSide, PieceKind.Rook, kingside: true)) newCastlingRights &= ~GetKingsideCastleFlag(ActiveSide);
                    if (source == GetStartLocation(ActiveSide, PieceKind.Rook, kingside: false)) newCastlingRights &= ~GetQueensideCastleFlag(ActiveSide);
                    break;
            }

            if (isCapture)
            {
                if (destination == GetStartLocation(OpposingSide, PieceKind.Rook, kingside: true))
                {
                    newCastlingRights &= ~GetKingsideCastleFlag(OpposingSide);
                }
                else if (destination == GetStartLocation(OpposingSide, PieceKind.Rook, kingside: false))
                {
                    newCastlingRights &= ~GetQueensideCastleFlag(OpposingSide);
                }
            }

            // Update en passant target

            bool isPawnAdvanceBy2 = (piece.Kind == PieceKind.Pawn && source.Rank == SecondRank(ActiveSide) && destination == source.Up(ForwardStep(ActiveSide) * 2));
            var newEnPassantTarget = isPawnAdvanceBy2 ? source.Up(ForwardStep(ActiveSide)) : (Location?)null;

            // Update halfmove clock and fullmove number

            int newHalfMoveClock = (isCapture || piece.Kind == PieceKind.Pawn) ? 0 : (HalfMoveClock + 1);
            int newFullMoveNumber = WhiteToMove ? FullMoveNumber : (FullMoveNumber + 1);

            // Finish updating the hash

            newHash ^= ZobristKey.ForActiveSide(ActiveSide);
            newHash ^= ZobristKey.ForActiveSide(OpposingSide);
            newHash ^= ZobristKey.ForCastlingRights(CastlingRights);
            newHash ^= ZobristKey.ForCastlingRights(newCastlingRights);
            if (EnPassantTarget.HasValue) newHash ^= ZobristKey.ForEnPassantFile(EnPassantTarget.Value.File);
            if (newEnPassantTarget.HasValue) newHash ^= ZobristKey.ForEnPassantFile(newEnPassantTarget.Value.File);

            var secrets = new Secrets(
                pieceMasks: newPieceMasks,
                hash: newHash);

            return new State(
                board: newBoard,
                activeSide: OpposingSide,
                castlingRights: newCastlingRights,
                enPassantTarget: newEnPassantTarget,
                halfMoveClock: newHalfMoveClock,
                fullMoveNumber: newFullMoveNumber,
                secrets: secrets);
        }

        private void ApplyInternal(
            ref Board board,
            ref PlayerProperty<PieceMasks> pieceMasks,
            ref ulong hash,
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
            var newBoard = Board.CreateBuilder(board);
            newBoard[source] = default;
            newBoard[destination] = newPiece;
            // Update piece masks
            var newPms = Pms.CreateBuilder(pieceMasks.Get(ActiveSide));
            newPms[piece.Kind] &= ~source.GetMask();
            newPms[newKind] |= destination.GetMask();
            pieceMasks = pieceMasks.Set(ActiveSide, newPms.Value);
            // Update hash
            hash ^= ZobristKey.ForPieceSquare(piece, source);
            hash ^= ZobristKey.ForPieceSquare(newPiece, destination);

            bool isCapture = Board[destination].HasPiece || isEnPassantCapture;
            if (isCapture)
            {
                var toClear = isEnPassantCapture
                    ? (piece.IsWhite ? destination.Down(1) : destination.Up(1))
                    : destination;
                var capturedPiece = Board[toClear].Piece;
                // Update board
                if (isEnPassantCapture) newBoard[toClear] = default;
                // Update piece masks
                var newOpposingPms = Pms.CreateBuilder(pieceMasks.Get(OpposingSide));
                newOpposingPms[capturedPiece.Kind] &= ~toClear.GetMask();
                pieceMasks = pieceMasks.Set(OpposingSide, newOpposingPms.Value);
                // Update hash
                hash ^= ZobristKey.ForPieceSquare(capturedPiece, toClear);
            }

            board = newBoard.Value;
        }

        public override bool Equals(object obj) => Equals(obj as State);

        public bool Equals([AllowNull] State other)
        {
            if (other == null) return false;

            if (!Board.Equals(other.Board) ||
                ActiveSide != other.ActiveSide ||
                CastlingRights != other.CastlingRights ||
                EnPassantTarget != other.EnPassantTarget ||
                HalfMoveClock != other.HalfMoveClock ||
                FullMoveNumber != other.FullMoveNumber)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.Add(Board);
            hc.Add(ActiveSide);
            hc.Add(CastlingRights);
            hc.Add(EnPassantTarget);
            hc.Add(HalfMoveClock);
            hc.Add(FullMoveNumber);
            return hc.ToHashCode();
        }

        public IEnumerable<Move> GetMoves() => GetSuccessors().Select(p => p.Move);

        public IEnumerable<SuccessorPair> GetSuccessors()
        {
            // todo: should this be lazy too?
            PooledList<Move> GetPossibleMoves(Location source)
            {
                // todo: enforce this should never reach capacity?
                var list = PooledList<Move>.Get(28);

                var destinations = GetPossibleDestinations(source);
                var piece = Board[source].Piece;
                for (var bb = destinations; bb != Bitboard.Zero; bb = bb.ClearLsb())
                {
                    var destination = bb.NextLocation();
                    // If we're a pawn moving to the back rank and promoting, there are multiple moves to consider
                    if (piece.Kind == PieceKind.Pawn && source.Rank == SeventhRank(ActiveSide))
                    {
                        list.Add(new Move(source, destination, promotionKind: PieceKind.Knight));
                        list.Add(new Move(source, destination, promotionKind: PieceKind.Bishop));
                        list.Add(new Move(source, destination, promotionKind: PieceKind.Rook));
                        list.Add(new Move(source, destination, promotionKind: PieceKind.Queen));
                    }
                    else
                    {
                        list.Add(new Move(source, destination));
                    }
                }

                return list;
            }

            foreach (var tile in ActivePlayer.GetOccupiedTiles())
            {
                using var movesToTry = GetPossibleMoves(tile.Location);
                foreach (var move in movesToTry)
                {
                    var succ = TryApply(move, out _);
                    if (succ != null) yield return (move, succ);
                }
            }
        }

        public Board.OccupiedTileEnumerator GetOccupiedTiles() => Board.GetOccupiedTiles();

        public PlayerState GetPlayer(Side side) => side.IsWhite() ? White : Black;

        public Board.TileEnumerator GetTiles() => Board.GetTiles();

        // todo: remove this from public api?
        public State SetActiveSide(Side value) => new State(this)
        {
            ActiveSide = value,
            Hash = Hash ^ ZobristKey.ForActiveSide(ActiveSide) ^ ZobristKey.ForActiveSide(value)
        };

        public override string ToString()
        {
            var fen = new StringBuilder();

            for (var rank = Rank8; rank >= Rank1; rank--)
            {
                int gap = 0;
                for (var file = FileA; file <= FileH; file++)
                {
                    var tile = this[file, rank];
                    if (!tile.HasPiece) gap++;
                    else
                    {
                        if (gap > 0) fen.Append(gap);
                        fen.Append(tile.Piece.ToDisplayChar());
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
        private bool CanAttackOpposingKing => FindKing(OpposingSide) is Location loc && IsAttackedBy(ActiveSide, loc);

        private bool CanCastleKingside => _canCastleKingside ?? (bool)(_canCastleKingside = CanCastleCore(kingside: true));
        private bool CanCastleQueenside => _canCastleQueenside ?? (bool)(_canCastleQueenside = CanCastleCore(kingside: false));

        private ulong InitZobristHash()
        {
            Debug.Assert(Board != null);

            ulong hash = 0;
            foreach (var tile in GetOccupiedTiles())
            {
                hash ^= ZobristKey.ForPieceSquare(tile.Piece, tile.Location);
            }

            hash ^= ZobristKey.ForActiveSide(ActiveSide);
            hash ^= ZobristKey.ForCastlingRights(CastlingRights);
            if (EnPassantTarget.HasValue) hash ^= ZobristKey.ForEnPassantFile(EnPassantTarget.Value.File);
            return hash;
        }

        private static PlayerProperty<PieceMasks> InitPieceMasks(Board board)
        {
            var (white, black) = (Pms.CreateBuilder(), Pms.CreateBuilder());
            foreach (var tile in board.GetOccupiedTiles())
            {
                var piece = tile.Piece;
                var pms = piece.IsWhite ? white : black;
                pms[piece.Kind] |= tile.Location.GetMask();
            }
            return (white.Value, black.Value);
        }
        
        private PlayerProperty<Bitboard> InitOccupies()
        {
            var (white, black) = (Bitboard.Zero, Bitboard.Zero);
            for (var kind = PieceKind.Pawn; kind <= PieceKind.King; kind++)
            {
                white |= PieceMasks.White[kind];
                black |= PieceMasks.Black[kind];
            }
            return (white, black);
        }

        private PlayerProperty<Bitboard> InitAttacks()
        {
            Debug.Assert(Board != null);

            var (white, black) = (Bitboard.Zero, Bitboard.Zero);
            for (var bb = Occupied; bb != Bitboard.Zero; bb = bb.ClearLsb())
            {
                var source = bb.NextLocation();
                var attacks = GetModifiedAttackBitboard(source);

                if (Occupies.White[source])
                {
                    white |= attacks;
                }
                else
                {
                    black |= attacks;
                }
            }

            return (white, black);
        }

        private bool CanCastleCore(bool kingside)
        {
            bool flag = kingside ? ActivePlayer.CanCastleKingside : ActivePlayer.CanCastleQueenside;
            if (!flag) return false;

            // the above flag does not account for situations that temporarily prevent castling, so check for those here
            // note: we don't guard against castling into check because that's taken care of later
            var kingSource = GetStartLocation(ActiveSide, PieceKind.King);
            var rookSource = GetStartLocation(ActiveSide, PieceKind.Rook, kingside);
            var kingDestination = kingside ? kingSource.Right(2) : kingSource.Left(2);

            bool piecesBetweenKingAndRook = GetLocationsBetween(kingSource, rookSource).Any(loc => Board[loc].HasPiece);
            bool kingPassesThroughAttackedLocation = GetLocationsBetween(kingSource, kingDestination).Any(loc => IsAttackedBy(OpposingSide, loc));
            return !(piecesBetweenKingAndRook || IsCheck || kingPassesThroughAttackedLocation);
        }

        internal Location? FindKing(Side side)
        {
            var pm = GetPlayer(side).GetPieceMask(PieceKind.King);
            return pm != Bitboard.Zero ? pm.NextLocation() : (Location?)null;
        }

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
                        (allowCastling && deltaX == 2 && deltaY == 0 && CanCastleKingside) ||
                        (allowCastling && deltaX == -2 && deltaY == 0 && CanCastleQueenside);
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

            return canMoveIfUnblocked && (!canPieceBeBlocked || GetLocationsBetween(source, destination).All(loc => !Board[loc].HasPiece));
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
        /// Returns a list of locations that the piece at <paramref name="source"/> may move to.
        /// Does not account for whether the move would be invalid because its king is currently in check.
        /// </summary>
        private Bitboard GetPossibleDestinations(Location source)
        {
            Debug.Assert(Board[source].HasPiece);

            var result = GetModifiedAttackBitboard(source);
            var piece = Board[source].Piece;

            var side = piece.Side;
            Debug.Assert(side == ActiveSide); // this assumption is only used when we check for castling availability

            result &= ~Occupies.Get(side); // we can't move to squares occupied by our own pieces

            switch (piece.Kind)
            {
                case PieceKind.Pawn:
                    // a pawn can only move to the left/right if it captures an opposing piece
                    var captureMask = Occupies.Get(side.Flip());
                    if (EnPassantTarget.HasValue) captureMask |= EnPassantTarget.Value.GetMask();
                    result &= captureMask;

                    // the attack vectors also don't include moving forward by 1/2, so OR those in
                    Debug.Assert(source.Rank != EighthRank(side)); // pawns should be promoted once they reach the eighth rank
                    var forward = ForwardStep(side);
                    var up1 = source.Up(forward);
                    if (!Occupied[up1])
                    {
                        result |= up1.GetMask();
                        if (source.Rank == SecondRank(side))
                        {
                            var up2 = source.Up(forward * 2);
                            if (!Occupied[up2]) result |= up2.GetMask();
                        }
                    }
                    break;
                case PieceKind.King:
                    if (CanCastleKingside) result |= source.Right(2).GetMask();
                    if (CanCastleQueenside) result |= source.Left(2).GetMask();
                    break;
            }

            return result;
        }

        /// <summary>
        /// Determines whether <paramref name="location"/> is attacked by an enemy piece.
        /// Ignores whether it's possible for the enemy piece to move (ie. because it is pinned to the enemy king).
        /// </summary>
        private bool IsAttackedBy(Side side, Location location) => GetPlayer(side).Attacks[location];

        // todo: use bitboards here
        /// <summary>
        /// Returns the tiles along a vertical, horizontal, or diagonal line between <paramref name="source"/> and <paramref name="destination"/>, exclusive.
        /// </summary>
        private static IEnumerable<Location> GetLocationsBetween(Location source, Location destination)
        {
            Debug.Assert(source != destination);
            var (deltaX, deltaY) = (destination.File - source.File, destination.Rank - source.Rank);

            if (deltaX == 0)
            {
                // Vertical
                var start = (deltaY > 0) ? source : destination;
                int shift = deltaY.Abs();
                for (int dy = 1; dy < shift; dy++)
                {
                    yield return start.Up(dy);
                }
            }
            else if (deltaY == 0)
            {
                // Horizontal
                var start = (deltaX > 0) ? source : destination;
                int shift = deltaX.Abs();
                for (int dx = 1; dx < shift; dx++)
                {
                    yield return start.Right(dx);
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
                    yield return start.Right(dx).Up(dy);
                }
            }
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

        #endregion
    }
}
