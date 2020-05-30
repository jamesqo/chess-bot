using ChessBot.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static ChessBot.Piece;

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
            PlayerColor activeColor,
            PlayerInfo white,
            PlayerInfo black,
            Location? enPassantTarget)
        {
            _board = board;
            ActiveColor = activeColor;
            White = white.SetState(this);
            Black = black.SetState(this);
            EnPassantTarget = enPassantTarget;
        }

        private State(State other) : this(
            other._board,
            other.ActiveColor,
            other.White,
            other.Black,
            other.EnPassantTarget)
        {
        }

        public static State ParseFen(string fen)
        {
            var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6) throw new InvalidFenException("Incorrect number of fields");

            var piecePlacement = parts[0];
            var activeColor = parts[1] switch
            {
                "w" => PlayerColor.White,
                "b" => PlayerColor.Black,
                _ => throw new InvalidFenException($"Invalid active color: {parts[1]}")
            };
            var castlingRights = parts[2];
            var enPassantTarget = parts[3] switch
            {
                "-" => (Location?)null,
                _ => Location.Parse(parts[3]) // todo: wrap AnParseException
            };
            var rule50Counter = parts[4]; // todo: don't ignore
            var fullMoveCounter = parts[5]; // todo: don't ignore

            var ranks = piecePlacement.Split('/');
            if (ranks.Length != 8) throw new InvalidFenException("Incorrect number of ranks");

            var board = ImmutableArray.CreateBuilder<Tile>(64);
            board.Count = 64;

            for (int r = 0; r < 8; r++)
            {
                string rank = ranks[7 - r];

                bool allowDigit = true;
                int c = 0;
                foreach (char ch in rank)
                {
                    if ((ch >= '1' && ch <= '8') && allowDigit)
                    {
                        int skip = (ch - '0');
                        if (c + skip > 8) throw new InvalidFenException("Incorrect number of files");
                        for (int i = 0; i < skip; i++)
                        {
                            var emptyTile = new Tile((c + i, r));
                            board[GetBoardIndex(c + i, r)] = emptyTile;
                        }
                        c += skip;
                        allowDigit = false;
                    }
                    else
                    {
                        if (c == 8) throw new InvalidFenException("Incorrect number of files");
                        var color = char.IsLower(ch) ? PlayerColor.Black : PlayerColor.White; // is this invariant?
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
                        var piece = new Piece(color, kind);
                        var tile = new Tile((c, r), piece);
                        board[GetBoardIndex(c, r)] = tile;
                        c++;
                        allowDigit = true;
                    }
                }

                if (c != 8) throw new InvalidFenException("Incorrect number of files");
            }

            var white = new PlayerInfo(PlayerColor.White, canCastleKingside: false, canCastleQueenside: false);
            var black = new PlayerInfo(PlayerColor.Black, canCastleKingside: false, canCastleQueenside: false);
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
                activeColor: activeColor,
                white: white,
                black: black,
                enPassantTarget: enPassantTarget);
        }

        private static int GetBoardIndex(int column, int row) => (8 * column + row);
        private static int GetBoardIndex(Location location) => GetBoardIndex(location.Column, location.Row);

        private readonly ImmutableArray<Tile> _board;
        private ImmutableArray<Tile> _occupiedTiles;

        public PlayerColor ActiveColor { get; private set; } // todo: remove this from public api?
        public PlayerInfo White { get; private set; }
        public PlayerInfo Black { get; private set; }
        public Location? EnPassantTarget { get; private set; }

        public State SetActiveColor(PlayerColor value) => new State(this) { ActiveColor = value };
        public State SetWhite(PlayerInfo value) => new State(this) { White = value };
        public State SetBlack(PlayerInfo value) => new State(this) { Black = value };
        public State SetEnPassantTarget(Location? value) => new State(this) { EnPassantTarget = value };

        public PlayerInfo ActivePlayer => GetPlayer(ActiveColor);
        public PlayerColor OpposingColor => WhiteToMove ? PlayerColor.Black : PlayerColor.White;
        public PlayerInfo OpposingPlayer => GetPlayer(OpposingColor);

        public bool IsCheck => GetKingsLocation(ActiveColor) is Location loc && IsAttackedBy(OpposingColor, loc);
        public bool IsCheckmate => IsCheck && IsTerminal;
        public bool IsStalemate => !IsCheck && IsTerminal;
        public bool IsTerminal => !GetMoves().Any();
        public bool WhiteToMove => ActiveColor == PlayerColor.White; // todo: use this everywhere

        private bool IsOpposingKingAttacked => GetKingsLocation(OpposingColor) is Location loc && IsAttackedBy(ActiveColor, loc);
        private int PieceCount => White.PieceCount + Black.PieceCount;

        public Tile this[int column, int row] => _board[GetBoardIndex(column, row)];
        public Tile this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[location.Column, location.Row];
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
            if (piece.Color != ActiveColor)
            {
                return Error("Piece's color does not match active player's color");
            }
            if (this[destination].HasPiece && this[destination].Piece.Color == piece.Color)
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
            int promotionRow = WhiteToMove ? 7 : 0;
            if (promotes != (piece.Kind == PieceKind.Pawn && destination.Row == promotionRow))
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
                    if (source == newActivePlayer.InitialKingsideRookLocation) newActivePlayer = newActivePlayer.SetCanCastleKingside(true);
                    if (source == newActivePlayer.InitialQueensideRookLocation) newActivePlayer = newActivePlayer.SetCanCastleQueenside(true);
                    break;
            }

            var newOpposingPlayer = OpposingPlayer;
            bool isEnPassantCapture = (piece.Kind == PieceKind.Pawn && EnPassantTarget.HasValue && destination == EnPassantTarget.Value);
            bool isCapture = this[destination].HasPiece || isEnPassantCapture;
            if (isCapture)
            {
                newOpposingPlayer = newOpposingPlayer.SetOccupiedTiles(default); // other player's occupied tiles have to be recomputed iff there's a capture
                newOpposingPlayer = newOpposingPlayer.SetPieceCount(newOpposingPlayer.PieceCount - 1);
            }

            bool is2Advance = (piece.Kind == PieceKind.Pawn && (WhiteToMove ? (destination == source.Up(2)) : (destination == source.Down(2))));
            var newEnPassantTarget = is2Advance ? (WhiteToMove ? source.Up(1) : source.Down(1)) : (Location?)null;

            // Step 3: Apply the changes and ensure our king isn't attacked afterwards
            newBoard = ApplyMoveInternal(newBoard, source, destination, move.PromotionKind, isEnPassantCapture);

            var result = new State(
                board: newBoard,
                activeColor: OpposingColor,
                white: WhiteToMove ? newActivePlayer : newOpposingPlayer,
                black: WhiteToMove ? newOpposingPlayer : newActivePlayer,
                enPassantTarget: newEnPassantTarget);

            if (result.IsOpposingKingAttacked) // note: this corresponds to the king that was active in the previous state
            {
                return Error($"Move is invalid since it lets {ActiveColor}'s king be attacked");
            }

            return Result(result);
        }

        private static ImmutableArray<Tile> ApplyMoveInternal(ImmutableArray<Tile> board, Location source, Location destination, PieceKind? promotionKind = null, bool isEnPassantCapture = false)
        {
            Debug.Assert(source != destination);

            var newBoard = board.ToBuilder();
            var (sourceIndex, destinationIndex) = (GetBoardIndex(source), GetBoardIndex(destination));

            Debug.Assert(board[sourceIndex].HasPiece);
            Debug.Assert(!board[destinationIndex].HasPiece || board[destinationIndex].Piece.Color != board[sourceIndex].Piece.Color);

            newBoard[sourceIndex] = newBoard[sourceIndex].SetPiece(null);
            var piece = board[sourceIndex].Piece;
            if (promotionKind != null) piece = new Piece(piece.Color, promotionKind.Value);
            newBoard[destinationIndex] = newBoard[destinationIndex].SetPiece(piece);

            if (isEnPassantCapture)
            {
                var target = piece.Color == PlayerColor.White ? destination.Down(1) : destination.Up(1);
                int targetIndex = GetBoardIndex(target);
                Debug.Assert(board[targetIndex].HasPiece && board[targetIndex].Piece.Color != board[sourceIndex].Piece.Color && board[targetIndex].Piece.Kind == PieceKind.Pawn);
                newBoard[targetIndex] = newBoard[targetIndex].SetPiece(null);
            }

            return newBoard.MoveToImmutable();
        }

        private bool CanCastle(bool kingside)
        {
            bool flag = kingside ? ActivePlayer.CanCastleKingside : ActivePlayer.CanCastleQueenside;
            if (!flag) return false;

            // The above flag does not account for moves that temporarily prevent castling
            var kingSource = ActivePlayer.InitialKingLocation;
            var kingDestination = (kingside ? kingSource.Right(2) : kingSource.Left(2));
            bool kingPassesThroughOccupiedOrAttackedLocation = GetLocationsBetween(kingSource, kingDestination).Any(loc => this[loc].HasPiece || IsAttackedBy(OpposingColor, loc));
            return !(IsCheck || kingPassesThroughOccupiedOrAttackedLocation);
        }

        public override bool Equals(object obj) => Equals(obj as State);

        public bool Equals([AllowNull] State other)
        {
            if (other == null) return false;

            if (ActiveColor != other.ActiveColor ||
                !White.Equals(other.White) ||
                !Black.Equals(other.Black) || 
                EnPassantTarget != other.EnPassantTarget)
            {
                return false;
            }

            for (int c = 0; c < 8; c++)
            {
                for (int r = 0; r < 8; r++)
                {
                    if (!this[c, r].Equals(other[c, r])) return false;
                }
            }

            return true;
        }

        public override int GetHashCode() => throw new NotImplementedException();

        public IEnumerable<Move> GetMoves() => GetMovesAndSuccessors().Select(t => t.move);

        public IEnumerable<(Move move, State state)> GetMovesAndSuccessors()
        {
            var movesToTry = ActivePlayer
                .GetOccupiedTiles()
                .Select(t => t.Location)
                .SelectMany(s => GetPossibleDestinations(s).Select(d => new Move(s, d)))
                .Append(Move.Castle(ActiveColor, kingside: true))
                .Append(Move.Castle(ActiveColor, kingside: false));

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

        public PlayerInfo GetPlayer(PlayerColor color) => (color == PlayerColor.White) ? White : Black;

        public IEnumerable<State> GetSuccessors() => GetMovesAndSuccessors().Select(t => t.state);

        public ImmutableArray<Tile> GetTiles() => _board;

        // todo: include fields of each playerinfo
        // todo: just output fen
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendJoin(Environment.NewLine, GetOccupiedTiles()).AppendLine();
            sb.Append("White: ").AppendLine(White.ToString());
            sb.Append("Black: ").AppendLine(Black.ToString());
            return sb.ToString();
        }

        internal Location? GetKingsLocation(PlayerColor color)
        {
            // todo: fix impl so it doesn't throw if there are 2+ matches
            return GetPlayer(color)
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

            if (destinationTile.HasPiece && destinationTile.Piece.Color == piece.Color)
            {
                return false;
            }

            bool canMoveIfUnblocked;
            bool canPieceBeBlocked = false;
            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);

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
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    int homeRow = (piece.Color == PlayerColor.White ? 1 : 6);
                    bool isValidAdvance = (!destinationTile.HasPiece && delta.x == 0 && (delta.y == forward || (delta.y == forward * 2 && source.Row == homeRow)));
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
                    if (source.Row > 0)
                    {
                        if (source.Column > 0) destinations.Add(source.Down(1).Left(1));
                        destinations.Add(source.Down(1));
                        if (source.Column < 7) destinations.Add(source.Down(1).Right(1));
                    }
                    if (source.Column > 0) destinations.Add(source.Left(1));
                    if (source.Column < 7) destinations.Add(source.Right(1));
                    if (source.Row < 7)
                    {
                        if (source.Column > 0) destinations.Add(source.Up(1).Left(1));
                        destinations.Add(source.Up(1));
                        if (source.Column < 7) destinations.Add(source.Up(1).Right(1));
                    }
                    break;
                case PieceKind.Knight:
                    if (source.Row > 0 && source.Column > 1) destinations.Add(source.Down(1).Left(2));
                    if (source.Row < 7 && source.Column > 1) destinations.Add(source.Up(1).Left(2));
                    if (source.Row > 0 && source.Column < 6) destinations.Add(source.Down(1).Right(2));
                    if (source.Row < 7 && source.Column < 6) destinations.Add(source.Up(1).Right(2));
                    if (source.Row > 1 && source.Column > 0) destinations.Add(source.Down(2).Left(1));
                    if (source.Row < 6 && source.Column > 0) destinations.Add(source.Up(2).Left(1));
                    if (source.Row > 1 && source.Column < 7) destinations.Add(source.Down(2).Right(1));
                    if (source.Row < 6 && source.Column < 7) destinations.Add(source.Up(2).Right(1));
                    break;
                // todo: support en passant captures
                case PieceKind.Pawn:
                    int forward = (piece.Color == PlayerColor.White ? 1 : -1);
                    int homeRow = (piece.Color == PlayerColor.White ? 1 : 6);
                    int backRow = (piece.Color == PlayerColor.White ? 7 : 0);

                    // Because pawns are automatically promoted at the back bank, we shouldn't have to do a bounds check here
                    Debug.Assert(source.Row != backRow);
                    var n1 = source.Up(forward);
                    if (!this[n1].HasPiece) destinations.Add(n1);
                    if (source.Row == homeRow)
                    {
                        var n2 = source.Up(forward * 2);
                        if (!this[n1].HasPiece && !this[n2].HasPiece) destinations.Add(n2);
                    }

                    if (source.Column > 0)
                    {
                        var nw = n1.Left(1);
                        if ((this[nw].HasPiece && this[nw].Piece.Color != piece.Color) || nw == EnPassantTarget)
                        {
                            destinations.Add(nw);
                        }
                    }

                    if (source.Column < 7)
                    {
                        var ne = n1.Right(1);
                        if ((this[ne].HasPiece && this[ne].Piece.Color != piece.Color) || ne == EnPassantTarget)
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

            return destinations.Where(d => !this[d].HasPiece || this[d].Piece.Color != piece.Color);
        }

        // note: May include squares occupied by friendly pieces
        private IEnumerable<Location> GetDiagonalExtension(Location source)
        {
            var prev = source;

            // Northeast
            while (prev.Row < 7 && prev.Column < 7)
            {
                var next = prev.Up(1).Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Southeast
            while (prev.Row > 0 && prev.Column < 7)
            {
                var next = prev.Down(1).Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Southwest
            while (prev.Row > 0 && prev.Column > 0)
            {
                var next = prev.Down(1).Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // Northwest
            while (prev.Row < 7 && prev.Column > 0)
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
            while (prev.Column < 7)
            {
                var next = prev.Right(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // West
            while (prev.Column > 0)
            {
                var next = prev.Left(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // North
            while (prev.Row < 7)
            {
                var next = prev.Up(1);
                yield return next;
                if (this[next].HasPiece) break;
                prev = next;
            }

            // South
            while (prev.Row > 0)
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
        private bool IsAttackedBy(PlayerColor color, Location location)
            // It's ok that IsMovePossible() ignores castling, since the rook/king cannot perform captures while castling.
            => GetPlayer(color).GetOccupiedTiles().Any(t => IsMovePossible(t.Location, location));

        /// <summary>
        /// Returns the tiles along a vertical, horizontal, or diagonal line between <paramref name="source"/> and <paramref name="destination"/>, exclusive.
        /// </summary>
        private static IEnumerable<Location> GetLocationsBetween(Location source, Location destination)
        {
            Debug.Assert(source != destination);
            var delta = (x: destination.Column - source.Column, y: destination.Row - source.Row);

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
