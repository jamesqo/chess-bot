using ChessBot.Exceptions;
using ChessBot.Types;
using ChessBot.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    public class State : IEquatable<State>
    {
        public static string StartFen { get; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        public static State Start { get; } = ParseFen(StartFen);

        private State(
            PlayerState white,
            PlayerState black,
            Side activeSide,
            Location? enPassantTarget,
            int halfMoveClock,
            int fullMoveNumber,
            ulong? hash = null)
        {
            _tiles = InitTiles(white, black);
            White = white;
            Black = black;
            ActiveSide = activeSide;
            EnPassantTarget = enPassantTarget;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
            Hash = hash ?? InitZobristHash();
        }

        // todo: remove this
        private State(State other) : this(
            other.White,
            other.Black,
            other.ActiveSide,
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
            var castlingRights = parts[2];
            var enPassantTarget = parts[3] switch
            {
                "-" => (Location?)null,
                _ => Location.TryParse(parts[3]) ?? throw new InvalidFenException($"Invalid en passant target: {parts[3]}")
            };
            if (!int.TryParse(parts[4], out var halfMoveClock) || halfMoveClock < 0) throw new InvalidFenException($"Invalid halfmove clock: {parts[4]}");
            if (!int.TryParse(parts[5], out var fullMoveNumber) || fullMoveNumber <= 0) throw new InvalidFenException($"Invalid fullmove number: {parts[5]}");

            var rankDescs = piecePlacement.Split('/');
            if (rankDescs.Length != 8) throw new InvalidFenException("Incorrect number of ranks");

            var bbs = new Bitboard[][] { new Bitboard[6], new Bitboard[6] };
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
                        bbs[(int)side][(int)kind] |= location.GetMask(); 
                        file++;
                        allowDigit = true;
                    }
                }

                if ((int)file != 8) throw new InvalidFenException("Incorrect number of files");
            }

            var white = new PlayerState(Side.White, bitboards: ImmutableArray.Create(bbs[0]), canCastleKingside: false, canCastleQueenside: false);
            var black = new PlayerState(Side.Black, bitboards: ImmutableArray.Create(bbs[1]), canCastleKingside: false, canCastleQueenside: false);
            if (castlingRights != "-")
            {
                foreach (char ch in castlingRights)
                {
                    switch (ch)
                    {
                        case 'K': white = white.SetCanCastleKingside(true); break;
                        case 'Q': white = white.SetCanCastleQueenside(true); break;
                        case 'k': black = black.SetCanCastleKingside(true); break;
                        case 'q': black = black.SetCanCastleQueenside(true); break;
                        default: throw new InvalidFenException($"Invalid castling flag: {ch}");
                    }
                }
            }

            return new State(
                white: white,
                black: black,
                activeSide: activeSide,
                enPassantTarget: enPassantTarget,
                halfMoveClock: halfMoveClock,
                fullMoveNumber: fullMoveNumber);
        }

        private TileList _tiles;
        private bool? _canCastleKingside;
        private bool? _canCastleQueenside;

        public PlayerState White { get; private set; }
        public PlayerState Black { get; private set; }
        public Side ActiveSide { get; private set; }
        public Location? EnPassantTarget { get; private set; }
        public int HalfMoveClock { get; private set; }
        public int FullMoveNumber { get; private set; }
        public ulong Hash { get; private set; }

        public PlayerState ActivePlayer => GetPlayer(ActiveSide);
        public Side OpposingSide => ActiveSide.Flip();
        public PlayerState OpposingPlayer => GetPlayer(OpposingSide);

        public bool IsCheck => GetKingsLocation(ActiveSide) is Location loc && IsAttackedBy(OpposingSide, loc);
        public bool IsCheckmate => IsCheck && IsTerminal;
        public bool IsStalemate => !IsCheck && IsTerminal;
        public bool IsTerminal => !GetMoves().Any();
        public bool WhiteToMove => ActiveSide.IsWhite();

        public Tile this[Location location] => _tiles[location];
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
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            var (source, destination) = (move.Source, move.Destination);
            if (!this[source].HasPiece)
            {
                error = new InvalidMoveException("Source tile is empty"); return null;
            }

            var piece = this[source].Piece;
            if (piece.Side != ActiveSide)
            {
                error = new InvalidMoveException("Piece's color does not match active player's color"); return null;
            }
            if (this[destination].HasPiece && this[destination].Piece.Side == piece.Side)
            {
                error = new InvalidMoveException("Destination tile is already occupied by a piece of the same color"); return null;
            }

            bool isCapture = this[destination].HasPiece || (piece.Kind == PieceKind.Pawn && destination == EnPassantTarget);
            if (move.IsCapture.HasValue && move.IsCapture.Value != isCapture)
            {
                error = new InvalidMoveException($"{nameof(move.IsCapture)} property is not set properly"); return null;
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

            if ((move.IsKingsideCastle || move.IsQueensideCastle) && !(move.IsKingsideCastle ? CanCastleKingside : CanCastleQueenside))
            {
                error = new InvalidMoveException("Requirements for castling not met"); return null;
            }

            var result = ApplyUnsafe(move);

            // Ensure our king isn't attacked afterwards

            // todo: as an optimization, we could narrow our search if our king is currently in check.
            // we may only bother for the three types of moves that could possibly get us out of check.

            if (result.CanAttackOpposingKing) // this corresponds to the king that was active in the previous state
            {
                error = new InvalidMoveException($"Move is invalid since it lets {ActiveSide}'s king be attacked"); return null;
            }

            if (move.IsCheck.HasValue && move.IsCheck.Value != result.IsCheck)
            {
                error = new InvalidMoveException($"{nameof(move.IsCheck)} property is not set properly"); return null;
            }

            if (move.IsCheckmate.HasValue && move.IsCheckmate.Value != result.IsCheckmate)
            {
                error = new InvalidMoveException($"{nameof(move.IsCheckmate)} property is not set properly"); return null;
            }

            error = null;
            return result;
        }

        private State ApplyUnsafe(Move move)
        {
            var (source, destination) = (move.Source, move.Destination);
            var piece = this[source].Piece;
            bool isEnPassantCapture = (piece.Kind == PieceKind.Pawn && destination == EnPassantTarget);
            bool isCapture = this[destination].HasPiece || isEnPassantCapture;

            ulong newHash = Hash;

            // Update the state of each player

            var newActive = ActivePlayer.SetOccupiedTiles(default); // occupied tiles have to be recomputed
            switch (piece.Kind)
            {
                case PieceKind.King:
                    newActive = newActive.SetCanCastleKingside(false).SetCanCastleQueenside(false);
                    break;
                case PieceKind.Rook:
                    // todo: we should also update these properties if the rook is captured, as opposed to being moved.
                    if (source == GetStartLocation(ActiveSide, PieceKind.Rook, kingside: true)) newActive = newActive.SetCanCastleKingside(false);
                    if (source == GetStartLocation(ActiveSide, PieceKind.Rook, kingside: false)) newActive = newActive.SetCanCastleQueenside(false);
                    break;
            }

            var newOpposing = OpposingPlayer;
            if (isCapture)
            {
                newOpposing = newOpposing.SetOccupiedTiles(default); // other player's occupied tiles have to be recomputed iff there's a capture

                if (destination == GetStartLocation(OpposingSide, PieceKind.Rook, kingside: true))
                {
                    newOpposing = newOpposing.SetCanCastleKingside(false);
                }
                else if (destination == GetStartLocation(OpposingSide, PieceKind.Rook, kingside: false))
                {
                    newOpposing = newOpposing.SetCanCastleQueenside(false);
                }
            }

            ApplyInternal(ref newActive, ref newOpposing, ref newHash, source, destination, move.PromotionKind, isEnPassantCapture);

            // Handle castling specially because we have to move the rook too

            if (move.IsKingsideCastle || move.IsQueensideCastle)
            {
                var rookSource = GetStartLocation(ActiveSide, PieceKind.Rook, move.IsKingsideCastle);
                var rookDestination = move.IsKingsideCastle ? rookSource.Left(2) : rookSource.Right(3);
                ApplyInternal(ref newActive, ref newOpposing, ref newHash, rookSource, rookDestination);
            }

            // Update other fields

            var newWhite = WhiteToMove ? newActive : newOpposing;
            var newBlack = WhiteToMove ? newOpposing : newActive;

            bool isPawnAdvanceBy2 = (piece.Kind == PieceKind.Pawn && source.Rank == SecondRank(ActiveSide) && destination == source.Up(ForwardStep(ActiveSide) * 2));
            var newEnPassantTarget = isPawnAdvanceBy2 ? source.Up(ForwardStep(ActiveSide)) : (Location?)null;

            int newHalfMoveClock = (isCapture || piece.Kind == PieceKind.Pawn) ? 0 : (HalfMoveClock + 1);
            int newFullMoveNumber = WhiteToMove ? FullMoveNumber : (FullMoveNumber + 1);

            newHash ^= ZobristKey.ForActiveSide(ActiveSide);
            newHash ^= ZobristKey.ForActiveSide(OpposingSide);
            newHash ^= ZobristKey.ForCastlingRights(
                White.CanCastleKingside,
                White.CanCastleQueenside,
                Black.CanCastleKingside,
                Black.CanCastleQueenside);
            newHash ^= ZobristKey.ForCastlingRights(
                newWhite.CanCastleKingside,
                newWhite.CanCastleQueenside,
                newBlack.CanCastleKingside,
                newBlack.CanCastleQueenside);
            if (EnPassantTarget.HasValue) newHash ^= ZobristKey.ForEnPassantFile(EnPassantTarget.Value.File);
            if (newEnPassantTarget.HasValue) newHash ^= ZobristKey.ForEnPassantFile(newEnPassantTarget.Value.File);

            return new State(
                activeSide: OpposingSide,
                white: newWhite,
                black: newBlack,
                enPassantTarget: newEnPassantTarget,
                halfMoveClock: newHalfMoveClock,
                fullMoveNumber: newFullMoveNumber,
                hash: newHash);
        }

        private void ApplyInternal(
            ref PlayerState active,
            ref PlayerState opposing,
            ref ulong hash,
            Location source,
            Location destination,
            PieceKind? promotionKind = null,
            bool isEnPassantCapture = false)
        {
            Debug.Assert(source != destination);
            Debug.Assert(this[source].HasPiece);
            Debug.Assert(!this[destination].HasPiece || this[destination].Piece.Side != this[source].Piece.Side);

            var piece = this[source].Piece;
            var newKind = promotionKind ?? piece.Kind;
            var newBbs = active.Bitboards.ToBuilder();
            newBbs[(int)piece.Kind] &= ~source.GetMask();
            newBbs[(int)newKind] |= destination.GetMask();
            active = active.SetBitboards(newBbs.MoveToImmutable());

            hash ^= ZobristKey.ForPieceSquare(piece, source);
            hash ^= ZobristKey.ForPieceSquare(new Piece(piece.Side, newKind), destination);

            bool isCapture = this[destination].HasPiece || isEnPassantCapture;
            if (isCapture)
            {
                var newOpposingBbs = opposing.Bitboards.ToBuilder();
                var toClear = isEnPassantCapture
                    ? (piece.IsWhite ? destination.Down(1) : destination.Up(1))
                    : destination;
                var capturedPiece = this[toClear].Piece;
                newOpposingBbs[(int)capturedPiece.Kind] &= ~toClear.GetMask();
                opposing = opposing.SetBitboards(newOpposingBbs.MoveToImmutable());

                hash ^= ZobristKey.ForPieceSquare(capturedPiece, toClear);
            }
        }

        public override bool Equals(object obj) => Equals(obj as State);

        public bool Equals([AllowNull] State other)
        {
            if (other == null) return false;

            if (!White.Equals(other.White) ||
                !Black.Equals(other.Black) ||
                ActiveSide != other.ActiveSide ||
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
            hc.Add(White);
            hc.Add(Black);
            hc.Add(ActiveSide);
            hc.Add(EnPassantTarget);
            hc.Add(HalfMoveClock);
            hc.Add(FullMoveNumber);
            return hc.ToHashCode();
        }

        public IEnumerable<Move> GetMoves() => GetMovesAndSuccessors().Select(t => t.move);

        public IEnumerable<(Move move, State state)> GetMovesAndSuccessors()
        {
            IEnumerable<Move> GetPossibleMoves(Location source)
            {
                var piece = this[source].Piece;
                foreach (var destination in GetPossibleDestinations(source))
                {
                    // If we're a pawn moving to the back rank and promoting, there are multiple moves to consider
                    if (piece.Kind == PieceKind.Pawn && source.Rank == SeventhRank(ActiveSide))
                    {
                        yield return new Move(source, destination, promotionKind: PieceKind.Knight);
                        yield return new Move(source, destination, promotionKind: PieceKind.Bishop);
                        yield return new Move(source, destination, promotionKind: PieceKind.Rook);
                        yield return new Move(source, destination, promotionKind: PieceKind.Queen);
                    }
                    else
                    {
                        bool isKingsideCastle = (piece.Kind == PieceKind.King && destination == source.Right(2));
                        bool isQueensideCastle = (piece.Kind == PieceKind.King && destination == source.Left(2));
                        yield return new Move(source, destination, isKingsideCastle: isKingsideCastle, isQueensideCastle: isQueensideCastle);
                    }
                }
            }

            var movesToTry = ActivePlayer
                .GetOccupiedTiles()
                .Select(t => t.Location)
                .SelectMany(s => GetPossibleMoves(s));

            foreach (var move in movesToTry)
            {
                var succ = ApplyUnsafe(move);
                if (!succ.CanAttackOpposingKing) yield return (move, succ);
            }
        }

        public OccupiedTilesEnumerator GetOccupiedTiles() => new OccupiedTilesEnumerator(_tiles);

        public PlayerState GetPlayer(Side side) => side.IsWhite() ? White : Black;

        public IEnumerable<State> GetSuccessors() => GetMovesAndSuccessors().Select(t => t.state);

        public TilesEnumerator GetTiles() => new TilesEnumerator(_tiles);

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
        private bool CanAttackOpposingKing => GetKingsLocation(OpposingSide) is Location loc && IsAttackedBy(ActiveSide, loc);

        private bool CanCastleKingside => _canCastleKingside ?? (bool)(_canCastleKingside = CanCastleCore(kingside: true));
        private bool CanCastleQueenside => _canCastleQueenside ?? (bool)(_canCastleQueenside = CanCastleCore(kingside: false));

        private static TileList InitTiles(PlayerState white, PlayerState black)
        {
            ulong value1 = 0, value2 = 0, value3 = 0, value4 = 0;
            for (int i = 0; i < Piece.NumberOfValues; i++)
            {
                var piece = Piece.FromIndex(i);
                int pieceValue = piece.Value + 1; // 0 represents an empty tile

                var (side, kind) = (piece.Side, piece.Kind);
                var bb = (side.IsWhite() ? white : black).Bitboards[(int)kind];

                if ((pieceValue & 1) != 0) value1 |= bb;
                if ((pieceValue & 2) != 0) value2 |= bb;
                if ((pieceValue & 4) != 0) value3 |= bb;
                if ((pieceValue & 8) != 0) value4 |= bb;
            }
            return new TileList(value1, value2, value3, value4);
        }

        private ulong InitZobristHash()
        {
            ulong hash = 0;
            foreach (var tile in GetOccupiedTiles())
            {
                hash ^= ZobristKey.ForPieceSquare(tile.Piece, tile.Location);
            }

            hash ^= ZobristKey.ForActiveSide(ActiveSide);
            hash ^= ZobristKey.ForCastlingRights(
                White.CanCastleKingside,
                White.CanCastleQueenside,
                Black.CanCastleKingside,
                Black.CanCastleQueenside);
            if (EnPassantTarget.HasValue) hash ^= ZobristKey.ForEnPassantFile(EnPassantTarget.Value.File);
            return hash;
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

            bool piecesBetweenKingAndRook = GetLocationsBetween(kingSource, rookSource).Any(loc => this[loc].HasPiece);
            bool kingPassesThroughAttackedLocation = GetLocationsBetween(kingSource, kingDestination).Any(loc => IsAttackedBy(OpposingSide, loc));
            return !(piecesBetweenKingAndRook || IsCheck || kingPassesThroughAttackedLocation);
        }

        internal Location? GetKingsLocation(Side side)
        {
            foreach (var tile in GetPlayer(side).GetOccupiedTiles())
            {
                // There should be at most one king
                if (tile.Piece.Kind == PieceKind.King) return tile.Location;
            }
            return null;
        }

        /// <summary>
        /// Checks whether it's possible to move the piece on <paramref name="source"/> to <paramref name="destination"/>.
        /// Ignores whether we would create an invalid position by putting our king in check.
        /// <br/>
        /// This is basically equivalent to checking whether GetPossibleDestinations(<paramref name="source"/>) contains <paramref name="destination"/>.
        /// </summary>
        internal bool IsMovePossible(Location source, Location destination, bool allowCastling = true)
        {
            Debug.Assert(this[source].HasPiece);

            if (source == destination)
            {
                return false;
            }

            var sourceTile = this[source];
            var destinationTile = this[destination];
            var piece = sourceTile.Piece;

            if (destinationTile.HasPiece && destinationTile.Piece.Side == piece.Side)
            {
                return false;
            }

            bool canMoveIfUnblocked;
            bool canPieceBeBlocked = false;
            var (deltaX, deltaY) = (destination.File - source.File, destination.Rank - source.Rank);
            var (deltaXAbs, deltaYAbs) = (deltaX.Abs(), deltaY.Abs());

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    canMoveIfUnblocked = (deltaXAbs == deltaYAbs);
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.King:
                    canMoveIfUnblocked = (deltaXAbs <= 1 && deltaYAbs <= 1) ||
                        (allowCastling && deltaX == 2 && deltaY == 0 && CanCastleKingside) ||
                        (allowCastling && deltaX == -2 && deltaY == 0 && CanCastleQueenside);
                    break;
                case PieceKind.Knight:
                    canMoveIfUnblocked = (deltaXAbs == 1 && deltaYAbs == 2) || (deltaXAbs == 2 && deltaYAbs == 1);
                    break;
                case PieceKind.Pawn:
                    var (forward, secondRank) = (ForwardStep(piece.Side), SecondRank(piece.Side));
                    bool isValidAdvance = (deltaX == 0 && (deltaY == forward || (deltaY == forward * 2 && source.Rank == secondRank)) && !destinationTile.HasPiece);
                    bool isValidCapture = ((deltaXAbs == 1 && deltaY == forward) && (destinationTile.HasPiece || destination == EnPassantTarget));

                    canMoveIfUnblocked = (isValidAdvance || isValidCapture);
                    canPieceBeBlocked = isValidAdvance;
                    break;
                case PieceKind.Queen:
                    canMoveIfUnblocked = (deltaX == 0 || deltaY == 0 || deltaXAbs == deltaYAbs);
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.Rook:
                    canMoveIfUnblocked = (deltaX == 0 || deltaY == 0);
                    canPieceBeBlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return canMoveIfUnblocked && (!canPieceBeBlocked || GetLocationsBetween(source, destination).All(loc => !this[loc].HasPiece));
        }

        /// <summary>
        /// Returns a list of locations that the piece at <paramref name="source"/> may move to.
        /// Does not account for whether the move would be invalid because its king is currently in check.
        /// </summary>
        private IEnumerable<Location> GetPossibleDestinations(Location source)
        {
            var sourceTile = this[source];
            var piece = sourceTile.Piece;
            var destinations = new List<Location>();

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    destinations.AddRange(GetDiagonalExtension(source));
                    break;
                case PieceKind.King:
                    if (source.Rank > Rank1)
                    {
                        if (source.File > FileA) destinations.Add(source.Down(1).Left(1));
                        destinations.Add(source.Down(1));
                        if (source.File < FileH) destinations.Add(source.Down(1).Right(1));
                    }
                    if (source.File > FileA) destinations.Add(source.Left(1));
                    if (source.File < FileH) destinations.Add(source.Right(1));
                    if (source.Rank < Rank8)
                    {
                        if (source.File > FileA) destinations.Add(source.Up(1).Left(1));
                        destinations.Add(source.Up(1));
                        if (source.File < FileH) destinations.Add(source.Up(1).Right(1));
                    }
                    if (CanCastleKingside) destinations.Add(source.Right(2));
                    if (CanCastleQueenside) destinations.Add(source.Left(2));
                    break;
                case PieceKind.Knight:
                    if (source.Rank > Rank1 && source.File > FileB) destinations.Add(source.Down(1).Left(2));
                    if (source.Rank < Rank8 && source.File > FileB) destinations.Add(source.Up(1).Left(2));
                    if (source.Rank > Rank1 && source.File < FileG) destinations.Add(source.Down(1).Right(2));
                    if (source.Rank < Rank8 && source.File < FileG) destinations.Add(source.Up(1).Right(2));
                    if (source.Rank > Rank2 && source.File > FileA) destinations.Add(source.Down(2).Left(1));
                    if (source.Rank < Rank7 && source.File > FileA) destinations.Add(source.Up(2).Left(1));
                    if (source.Rank > Rank2 && source.File < FileH) destinations.Add(source.Down(2).Right(1));
                    if (source.Rank < Rank7 && source.File < FileH) destinations.Add(source.Up(2).Right(1));
                    break;
                case PieceKind.Pawn:
                    var (forward, secondRank, eighthRank) = (ForwardStep(piece.Side), SecondRank(piece.Side), EighthRank(piece.Side));

                    // Because pawns are automatically promoted at the back bank, we shouldn't have to do a bounds check here
                    Debug.Assert(source.Rank != eighthRank);
                    var n1 = source.Up(forward);
                    if (!this[n1].HasPiece) destinations.Add(n1);
                    if (source.Rank == secondRank)
                    {
                        var n2 = source.Up(forward * 2);
                        if (!this[n1].HasPiece && !this[n2].HasPiece) destinations.Add(n2);
                    }

                    if (source.File > FileA)
                    {
                        var nw = n1.Left(1);
                        if ((this[nw].HasPiece && this[nw].Piece.Side != piece.Side) || nw == EnPassantTarget)
                        {
                            destinations.Add(nw);
                        }
                    }

                    if (source.File < FileH)
                    {
                        var ne = n1.Right(1);
                        if ((this[ne].HasPiece && this[ne].Piece.Side != piece.Side) || ne == EnPassantTarget)
                        {
                            destinations.Add(ne);
                        }
                    }
                    break;
                case PieceKind.Queen:
                    destinations.AddRange(GetDiagonalExtension(source));
                    destinations.AddRange(GetOrthogonalExtension(source));
                    break;
                case PieceKind.Rook:
                    destinations.AddRange(GetOrthogonalExtension(source));
                    break;
            }

            return destinations.Where(d => !this[d].HasPiece || this[d].Piece.Side != piece.Side);
        }

        // note: May include squares occupied by friendly pieces
        private IEnumerable<Location> GetDiagonalExtension(Location source)
        {
            var prev = source;

            // Northeast
            while (prev.Rank < Rank8 && prev.File < FileH)
            {
                var next = prev.Up(1).Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Southeast
            while (prev.Rank > Rank1 && prev.File < FileH)
            {
                var next = prev.Down(1).Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Southwest
            while (prev.Rank > Rank1 && prev.File > FileA)
            {
                var next = prev.Down(1).Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Northwest
            while (prev.Rank < Rank8 && prev.File > FileA)
            {
                var next = prev.Up(1).Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }
        }

        // note: May include squares occupied by friendly pieces
        private IEnumerable<Location> GetOrthogonalExtension(Location source)
        {
            var prev = source;

            // East
            while (prev.File < FileH)
            {
                var next = prev.Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // West
            while (prev.File > FileA)
            {
                var next = prev.Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // North
            while (prev.Rank < Rank8)
            {
                var next = prev.Up(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // South
            while (prev.Rank > Rank1)
            {
                var next = prev.Down(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }
        }

        /// <summary>
        /// Determines whether <paramref name="location"/> is attacked by an enemy piece.
        /// Ignores whether it's possible for the enemy piece to move (ie. because it is pinned to the enemy king).
        /// </summary>
        private bool IsAttackedBy(Side side, Location location)
        {
            foreach (var tile in GetPlayer(side).GetOccupiedTiles())
            {
                // allowCastling = false is a small optimization, as the rook / king cannot perform captures while castling.
                if (IsMovePossible(tile.Location, location, allowCastling: false)) return true;
            }
            return false;
        }

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

        #endregion
    }
}
