using ChessBot.Exceptions;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
            ImmutableArray<Tile> board,
            Side activeSide,
            PlayerInfo white,
            PlayerInfo black,
            Location? enPassantTarget,
            int halfMoveClock,
            int fullMoveNumber)
        {
            _board = board;
            ActiveSide = activeSide;
            White = white.SetState(this);
            Black = black.SetState(this);
            EnPassantTarget = enPassantTarget;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
        }

        private State(State other) : this(
            other._board,
            other.ActiveSide,
            other.White,
            other.Black,
            other.EnPassantTarget,
            other.HalfMoveClock,
            other.FullMoveNumber)
        {
        }

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

            var board = ImmutableArray.CreateBuilder<Tile>(64);
            board.Count = 64;

            for (var rank = Rank1; rank <= Rank8; rank++)
            {
                string rankDesc = rankDescs[7 - (int)rank];

                var file = FileA;
                bool allowDigit = true;
                foreach (char ch in rankDesc)
                {
                    if ((ch >= '1' && ch <= '8') && allowDigit)
                    {
                        int skip = (ch - '0');
                        if ((int)file + skip > 8) throw new InvalidFenException("Incorrect number of files");

                        for (int i = 0; i < skip; i++)
                        {
                            var emptyTile = new Tile((file + i, rank));
                            board[GetBoardIndex(file + i, rank)] = emptyTile;
                        }

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
                        var tile = new Tile((file, rank), piece);
                        board[GetBoardIndex(file, rank)] = tile;
                        file++;
                        allowDigit = true;
                    }
                }

                if ((int)file != 8) throw new InvalidFenException("Incorrect number of files");
            }

            var white = new PlayerInfo(Side.White, canCastleKingside: false, canCastleQueenside: false);
            var black = new PlayerInfo(Side.Black, canCastleKingside: false, canCastleQueenside: false);
            if (castlingRights != "-")
            {
                foreach (char ch in castlingRights)
                {
                    // todo: we should keep track of whether we've seen duplicates, eg. 'KKqkq'
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
                board: board.MoveToImmutable(),
                activeSide: activeSide,
                white: white,
                black: black,
                enPassantTarget: enPassantTarget,
                halfMoveClock: halfMoveClock,
                fullMoveNumber: fullMoveNumber);
        }

        private static int GetBoardIndex(File file, Rank rank) => (8 * (int)file + (int)rank);
        private static int GetBoardIndex(Location location) => GetBoardIndex(location.File, location.Rank);

        private readonly ImmutableArray<Tile> _board;
        private ImmutableArray<Tile> _occupiedTiles;

        public Side ActiveSide { get; private set; } // todo: remove this from public api?
        public PlayerInfo White { get; private set; }
        public PlayerInfo Black { get; private set; }
        public Location? EnPassantTarget { get; private set; }
        public int HalfMoveClock { get; private set; }
        public int FullMoveNumber { get; private set; }

        public State SetActiveSide(Side value) => new State(this) { ActiveSide = value };
        //public State SetWhite(PlayerInfo value) => new State(this) { White = value };
        //public State SetBlack(PlayerInfo value) => new State(this) { Black = value };
        //public State SetEnPassantTarget(Location? value) => new State(this) { EnPassantTarget = value };
        //SetHalfMoveClock()
        //SetFullMoveNumber()

        public PlayerInfo ActivePlayer => GetPlayer(ActiveSide);
        public Side OpposingSide => WhiteToMove ? Side.Black : Side.White;
        public PlayerInfo OpposingPlayer => GetPlayer(OpposingSide);

        public bool IsCheck => GetKingsLocation(ActiveSide) is Location loc && IsAttackedBy(OpposingSide, loc);
        public bool IsCheckmate => IsCheck && IsTerminal;
        public bool IsStalemate => !IsCheck && IsTerminal;
        public bool IsTerminal => !GetMoves().Any();
        public bool WhiteToMove => ActiveSide == Side.White; // todo: use this everywhere

        private bool IsOpposingKingAttacked => GetKingsLocation(OpposingSide) is Location loc && IsAttackedBy(ActiveSide, loc);
        private int PieceCount => White.PieceCount + Black.PieceCount;

        public Tile this[File file, Rank rank] => _board[GetBoardIndex(file, rank)];
        public Tile this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[location.File, location.Rank];
        }
        public Tile this[string location] => this[Location.Parse(location)];

        public State ApplyMove(string move) => ApplyMove(Move.Parse(move, this));

        public State ApplyMove(Move move)
        {
            var (newState, error) = TryApplyMove(move);
            if (error != null) throw new InvalidMoveException(error);
            return newState;
        }

        public (State newState, string error) TryApplyMove(string move) => TryApplyMove(Move.Parse(move, this));

        // todo: instead of a string, error should be some kind of enum type
        public (State newState, string error) TryApplyMove(Move move)
        {
            (State, string) Result(State newState) => (newState, null);
            (State, string) Error(string error) => (null, error);

            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            var (source, destination) = (move.Source, move.Destination);
            if (!this[source].HasPiece)
            {
                return Error("Source tile is empty");
            }

            var piece = this[source].Piece;
            if (piece.Side != ActiveSide)
            {
                return Error("Piece's color does not match active player's color");
            }
            if (this[destination].HasPiece && this[destination].Piece.Side == piece.Side)
            {
                return Error("Destination tile is already occupied by a piece of the same color");
            }
            if (move.IsCapture.HasValue && move.IsCapture.Value != this[destination].HasPiece)
            {
                if (!(move.IsCapture.Value && piece.Kind == PieceKind.Pawn && EnPassantTarget.HasValue && destination == EnPassantTarget.Value))
                {
                    return Error($"{nameof(move.IsCapture)} property is not set properly");
                }
            }
            bool promotes = (move.PromotionKind != null);
            var promotionRank = WhiteToMove ? Rank8 : Rank1;
            if (promotes != (piece.Kind == PieceKind.Pawn && destination.Rank == promotionRank))
            {
                return Error("A promotion happens iff a pawn moves to the back rank");
            }

            // Step 1: Check that the move is valid movement-wise
            var newBoard = _board;

            if (move.IsKingsideCastle || move.IsQueensideCastle)
            {
                bool canCastle = CanCastle(move.IsKingsideCastle);
                if (!canCastle)
                {
                    return Error("Requirements for castling not met");
                }

                // Move the rook
                var rookSource = move.IsKingsideCastle ? ActivePlayer.InitialKingsideRookLocation : ActivePlayer.InitialQueensideRookLocation;
                var rookDestination = move.IsKingsideCastle ? rookSource.Left(2) : rookSource.Right(3);
                newBoard = ApplyMoveInternal(newBoard, rookSource, rookDestination);
            }
            else if (!IsMovePossible(source, destination))
            {
                return Error($"Movement rules do not allow {piece} to be brought from {source} to {destination}");
            }
            // todo: as an optimization, we could narrow our search if our king is currently in check.
            // we may only bother for the three types of moves that could possibly get us out of check.

            // Step 2: Update player infos
            var newActivePlayer = ActivePlayer.SetOccupiedTiles(default); // occupied tiles have to be recomputed

            switch (piece.Kind)
            {
                case PieceKind.King:
                    newActivePlayer = newActivePlayer.SetCanCastleKingside(false).SetCanCastleQueenside(false);
                    break;
                case PieceKind.Rook:
                    // todo: we should also update these properties if the rook is captured, as opposed to being moved.
                    if (source == newActivePlayer.InitialKingsideRookLocation) newActivePlayer = newActivePlayer.SetCanCastleKingside(false);
                    if (source == newActivePlayer.InitialQueensideRookLocation) newActivePlayer = newActivePlayer.SetCanCastleQueenside(false);
                    break;
            }

            var newOpposingPlayer = OpposingPlayer;
            bool isEnPassantCapture = (piece.Kind == PieceKind.Pawn && EnPassantTarget.HasValue && destination == EnPassantTarget.Value);
            bool isCapture = this[destination].HasPiece || isEnPassantCapture;
            if (isCapture)
            {
                newOpposingPlayer = newOpposingPlayer.SetOccupiedTiles(default); // other player's occupied tiles have to be recomputed iff there's a capture
                newOpposingPlayer = newOpposingPlayer.SetPieceCount(newOpposingPlayer.PieceCount - 1);

                if (destination == OpposingPlayer.InitialKingsideRookLocation)
                {
                    newOpposingPlayer = newOpposingPlayer.SetCanCastleKingside(false);
                }
                else if (destination == OpposingPlayer.InitialQueensideRookLocation)
                {
                    newOpposingPlayer = newOpposingPlayer.SetCanCastleQueenside(false);
                }
            }

            var pawnRank = WhiteToMove ? Rank2 : Rank7;
            bool is2Advance = (piece.Kind == PieceKind.Pawn && source.Rank == pawnRank && (WhiteToMove ? (destination == source.Up(2)) : (destination == source.Down(2))));
            var newEnPassantTarget = is2Advance ? (WhiteToMove ? source.Up(1) : source.Down(1)) : (Location?)null;

            int newHalfMoveClock = (isCapture || piece.Kind == PieceKind.Pawn) ? 0 : (HalfMoveClock + 1);
            int newFullMoveNumber = WhiteToMove ? FullMoveNumber : (FullMoveNumber + 1);

            // Step 3: Apply the changes and ensure our king isn't attacked afterwards
            newBoard = ApplyMoveInternal(newBoard, source, destination, move.PromotionKind, isEnPassantCapture);

            var result = new State(
                board: newBoard,
                activeSide: OpposingSide,
                white: WhiteToMove ? newActivePlayer : newOpposingPlayer,
                black: WhiteToMove ? newOpposingPlayer : newActivePlayer,
                enPassantTarget: newEnPassantTarget,
                halfMoveClock: newHalfMoveClock,
                fullMoveNumber: newFullMoveNumber);

            if (result.IsOpposingKingAttacked) // note: this corresponds to the king that was active in the previous state
            {
                return Error($"Move is invalid since it lets {ActiveSide}'s king be attacked");
            }

            return Result(result);
        }

        private static ImmutableArray<Tile> ApplyMoveInternal(ImmutableArray<Tile> board, Location source, Location destination, PieceKind? promotionKind = null, bool isEnPassantCapture = false)
        {
            Debug.Assert(source != destination);

            var newBoard = board.ToBuilder();
            var (sourceIndex, destinationIndex) = (GetBoardIndex(source), GetBoardIndex(destination));

            Debug.Assert(board[sourceIndex].HasPiece);
            Debug.Assert(!board[destinationIndex].HasPiece || board[destinationIndex].Piece.Side != board[sourceIndex].Piece.Side);

            newBoard[sourceIndex] = newBoard[sourceIndex].SetPiece(null);
            var piece = board[sourceIndex].Piece;
            if (promotionKind != null) piece = new Piece(piece.Side, promotionKind.Value);
            newBoard[destinationIndex] = newBoard[destinationIndex].SetPiece(piece);

            if (isEnPassantCapture)
            {
                var target = piece.Side == Side.White ? destination.Down(1) : destination.Up(1);
                int targetIndex = GetBoardIndex(target);
                Debug.Assert(board[targetIndex].HasPiece && board[targetIndex].Piece.Side != board[sourceIndex].Piece.Side && board[targetIndex].Piece.Kind == PieceKind.Pawn);
                newBoard[targetIndex] = newBoard[targetIndex].SetPiece(null);
            }

            return newBoard.MoveToImmutable();
        }

        private bool CanCastle(bool kingside)
        {
            bool flag = kingside ? ActivePlayer.CanCastleKingside : ActivePlayer.CanCastleQueenside;
            if (!flag) return false;

            // note: the above flag does not account for moves that temporarily prevent castling
            // note: we don't guard against castling into check because that's taken care of later
            // todo: if we're castling queenside, the b1 square may be occupied, but not attacked
            var kingSource = ActivePlayer.InitialKingLocation;
            var rookSource = (kingside ? ActivePlayer.InitialKingsideRookLocation : ActivePlayer.InitialQueensideRookLocation);
            bool piecesBetweenKingAndRook = GetLocationsBetween(kingSource, rookSource).Any(loc => this[loc].HasPiece);
            var kingDestination = (kingside ? kingSource.Right(2) : kingSource.Left(2));
            bool kingPassesThroughAttackedLocation = GetLocationsBetween(kingSource, kingDestination).Any(loc => IsAttackedBy(OpposingSide, loc));
            return !(piecesBetweenKingAndRook || IsCheck || kingPassesThroughAttackedLocation);
        }

        public override bool Equals(object obj) => Equals(obj as State);

        public bool Equals([AllowNull] State other)
        {
            if (other == null) return false;

            if (ActiveSide != other.ActiveSide ||
                !White.Equals(other.White) ||
                !Black.Equals(other.Black) || 
                EnPassantTarget != other.EnPassantTarget ||
                HalfMoveClock != other.HalfMoveClock ||
                FullMoveNumber != other.FullMoveNumber)
            {
                return false;
            }

            for (var file = FileA; file <= FileH; file++)
            {
                for (var rank = Rank1; rank <= Rank8; rank++)
                {
                    if (!this[file, rank].Equals(other[file, rank])) return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            for (var file = FileA; file <= FileH; file++)
            {
                for (var rank = Rank1; rank <= Rank8; rank++)
                {
                    hc.Add(this[file, rank]);
                }
            }

            hc.Add(ActiveSide);
            hc.Add(White.CanCastleKingside);
            hc.Add(White.CanCastleQueenside);
            hc.Add(Black.CanCastleKingside);
            hc.Add(Black.CanCastleQueenside);
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
                foreach (var destination in GetPossibleDestinations(source))
                {
                    // If we're a pawn moving to the back rank and promoting, there are multiple moves to consider
                    var promotionRank = WhiteToMove ? Rank7 : Rank2;
                    if (source.Rank == promotionRank && this[source].Piece.Kind == PieceKind.Pawn)
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

            var movesToTry = ActivePlayer
                .GetOccupiedTiles()
                .Select(t => t.Location)
                .SelectMany(s => GetPossibleMoves(s))
                .Append(Move.Castle(ActiveSide, kingside: true))
                .Append(Move.Castle(ActiveSide, kingside: false));

            foreach (var move in movesToTry)
            {
                var (newState, error) = TryApplyMove(move);
                if (error == null)
                {
                    yield return (move, newState);
                }
            }
        }

        public ImmutableArray<Tile> GetOccupiedTiles()
        {
            if (_occupiedTiles.IsDefault)
            {
                var builder = ImmutableArray.CreateBuilder<Tile>(PieceCount);
                foreach (var tile in GetTiles())
                {
                    if (tile.HasPiece)
                    {
                        builder.Add(tile);
                    }
                }
                _occupiedTiles = builder.MoveToImmutable();
            }
            return _occupiedTiles;
        }

        public PlayerInfo GetPlayer(Side side) => (side == Side.White) ? White : Black;

        public IEnumerable<State> GetSuccessors() => GetMovesAndSuccessors().Select(t => t.state);

        public ImmutableArray<Tile> GetTiles() => _board;

        public override string ToString()
        {
            // todo: this should be an ext method. this code is repeated elsewhere
            char ToChar(Piece piece)
            {
                char result = piece.Kind switch
                {
                    PieceKind.Pawn => 'P',
                    PieceKind.Knight => 'N',
                    PieceKind.Bishop => 'B',
                    PieceKind.Rook => 'R',
                    PieceKind.Queen => 'Q',
                    PieceKind.King => 'K',
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (piece.Side == Side.Black)
                {
                    result = char.ToLowerInvariant(result);
                }
                return result;
            }

            var fen = new StringBuilder();

            for (var rank = Rank8; rank >= Rank1; rank--)
            {
                int gap = 0;
                for (var file = FileA; file <= FileH; file++)
                {
                    var tile = this[file, rank];
                    if (!tile.HasPiece)
                    {
                        gap++;
                    }
                    else
                    {
                        if (gap > 0) fen.Append(gap);
                        fen.Append(ToChar(tile.Piece));
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

        internal Location? GetKingsLocation(Side side)
        {
            // todo: fix impl so it doesn't throw if there are 2+ matches
            return GetPlayer(side)
                .GetOccupiedTiles()
                .SingleOrDefault(t => t.Piece.Kind == PieceKind.King)?
                .Location;
        }

        /// <summary>
        /// Checks whether it's possible to move the piece on <paramref name="source"/> to <paramref name="destination"/>.
        /// Ignores whether we would create an invalid position by putting our king in check.
        /// <br/>
        /// This is basically equivalent to checking whether GetPossibleDestinations(<paramref name="source"/>) contains <paramref name="destination"/>.
        /// </summary>
        internal bool IsMovePossible(Location source, Location destination)
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
            var delta = (x: destination.File - source.File, y: destination.Rank - source.Rank);

            switch (piece.Kind)
            {
                case PieceKind.Bishop:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == Math.Abs(delta.y));
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.King:
                    // note: We ignore the possibility of castling since we already have logic in place to handle that
                    canMoveIfUnblocked = (Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1);
                    break;
                case PieceKind.Knight:
                    canMoveIfUnblocked = (Math.Abs(delta.x) == 1 && Math.Abs(delta.y) == 2) || (Math.Abs(delta.x) == 2 && Math.Abs(delta.y) == 1);
                    break;
                case PieceKind.Pawn:
                    int forward = (piece.Side == Side.White ? 1 : -1);
                    var homeRank = (piece.Side == Side.White ? Rank2 : Rank7);
                    bool isValidAdvance = (!destinationTile.HasPiece && delta.x == 0 && (delta.y == forward || (delta.y == forward * 2 && source.Rank == homeRank)));
                    bool isValidCapture = (destinationTile.HasPiece || destination == EnPassantTarget) && (Math.Abs(delta.x) == 1 && delta.y == forward); // todo: support en passant captures

                    canMoveIfUnblocked = (isValidAdvance || isValidCapture);
                    canPieceBeBlocked = isValidAdvance;
                    break;
                case PieceKind.Queen:
                    canMoveIfUnblocked = (delta.x == 0 || delta.y == 0 || Math.Abs(delta.x) == Math.Abs(delta.y));
                    canPieceBeBlocked = true;
                    break;
                case PieceKind.Rook:
                    canMoveIfUnblocked = (delta.x == 0 || delta.y == 0);
                    canPieceBeBlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // todo (perf): All is a bottleneck
            return canMoveIfUnblocked && (!canPieceBeBlocked || GetLocationsBetween(source, destination).All(loc => !this[loc].HasPiece));
        }

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
                    // Again, we don't handle castling here; that's taken care of directly by the caller.
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
                // todo: support en passant captures
                case PieceKind.Pawn:
                    int forward = (piece.Side == Side.White ? 1 : -1);
                    var homeRank = (piece.Side == Side.White ? Rank2 : Rank7);
                    var backRank = (piece.Side == Side.White ? Rank8 : Rank1);

                    // Because pawns are automatically promoted at the back bank, we shouldn't have to do a bounds check here
                    Debug.Assert(source.Rank != backRank);
                    var n1 = source.Up(forward);
                    if (!this[n1].HasPiece) destinations.Add(n1);
                    if (source.Rank == homeRank)
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
            // It's ok that IsMovePossible() ignores castling, since the rook/king cannot perform captures while castling.
            => GetPlayer(side).GetOccupiedTiles().Any(t => IsMovePossible(t.Location, location));

        /// <summary>
        /// Returns the tiles along a vertical, horizontal, or diagonal line between <paramref name="source"/> and <paramref name="destination"/>, exclusive.
        /// </summary>
        private static IEnumerable<Location> GetLocationsBetween(Location source, Location destination)
        {
            Debug.Assert(source != destination);
            var delta = (x: destination.File - source.File, y: destination.Rank - source.Rank);

            if (delta.x == 0)
            {
                // Vertical
                var start = (delta.y > 0) ? source : destination;
                int shift = Math.Abs(delta.y);
                for (int dy = 1; dy < shift; dy++)
                {
                    yield return start.Up(dy);
                }
            }
            else if (delta.y == 0)
            {
                // Horizontal
                var start = (delta.x > 0) ? source : destination;
                int shift = Math.Abs(delta.x);
                for (int dx = 1; dx < shift; dx++)
                {
                    yield return start.Right(dx);
                }
            }
            else
            {
                // Diagonal
                Debug.Assert(Math.Abs(delta.x) == Math.Abs(delta.y));

                var start = (delta.x > 0) ? source : destination;
                int shift = Math.Abs(delta.x);
                int slope = (delta.x == delta.y) ? 1 : -1;
                for (int dx = 1; dx < shift; dx++)
                {
                    int dy = dx * slope;
                    yield return start.Right(dx).Up(dy);
                }
            }
        }
    }
}
