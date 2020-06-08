using ChessBot.Exceptions;
using ChessBot.Helpers;
using ChessBot.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static ChessBot.Types.File;
using static ChessBot.Types.Rank;

namespace ChessBot
{
    /// <summary>
    /// Immutable class representing the state of a chess game.
    /// </summary>
    public class State : IState, IEquatable<State>
    {
        public static string StartFen { get; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        public static State Start { get; } = ParseFen(StartFen);

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
            var board = new Board();

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

            return new State(new MutState(
                board: in board,
                activeSide: activeSide,
                castlingRights: castlingRights,
                enPassantTarget: enPassantTarget,
                halfMoveClock: halfMoveClock,
                fullMoveNumber: fullMoveNumber));
        }

        private readonly MutState _inner;

        internal State(MutState inner) => _inner = inner;

        internal MutState Inner => _inner;

        #region Forwarded members

        /// <summary>
        /// The board state.
        /// </summary>
        public ref readonly Board Board => ref _inner.Board;

        /// <summary>
        /// The next side to move.
        /// </summary>
        public Side ActiveSide => _inner.ActiveSide;

        /// <summary>
        /// The castling rights for both players.
        /// </summary>
        public CastlingRights CastlingRights => _inner.CastlingRights;

        /// <summary>
        /// The destination square of an en passant capture if a pawn made a two-square move during the last turn, otherwise <see langword="null"/>.
        /// </summary>
        public Location? EnPassantTarget => _inner.EnPassantTarget;

        /// <summary>
        /// The number of halfmoves since the last capture or pawn advance.
        /// Used to determine if a draw can be claimed under the 50-move rule.
        /// </summary>
        public int HalfMoveClock => _inner.HalfMoveClock;

        /// <summary>
        /// The number of fullmoves since the start of the game.
        /// </summary>
        public int FullMoveNumber => _inner.FullMoveNumber;

        /// <summary>
        /// The Zobrist hash value for this state.
        /// </summary>
        public ulong Hash => _inner.Hash;

        /// <summary>
        /// Contains information about the white player.
        /// </summary>
        public Player White => _inner.White;

        /// <summary>
        /// Contains information about the black player.
        /// </summary>
        public Player Black => _inner.Black;

        public bool IsCheck => _inner.IsCheck;
        public Bitboard Occupied => _inner.Occupied;

        public bool WhiteToMove => _inner.WhiteToMove;
        public Side OpposingSide => _inner.OpposingSide;
        public Player ActivePlayer => _inner.ActivePlayer;
        public Player OpposingPlayer => _inner.OpposingPlayer;

        public Player GetPlayer(Side side) => _inner.GetPlayer(side);

        #endregion

        // note: these properties are very expensive to compute
        public bool IsCheckmate => IsCheck && IsTerminal;
        public bool IsStalemate => !IsCheck && IsTerminal;
        public bool IsTerminal => !GetSuccessors().Any();

        public Tile this[Location location] => new Tile(location, _inner.Board[location]);
        public Tile this[File file, Rank rank] => this[(file, rank)];
        public Tile this[string location] => this[Location.Parse(location)];

        public State Apply(string move) => Apply(Move.Parse(move, this));
        public State Apply(Move move) => TryApply(move, out var error) ?? throw new InvalidMoveException(error);

        public State? TryApply(string move, out InvalidMoveReason error)
        {
            Move moveObj;
            try
            {
                moveObj = Move.Parse(move, this);
            }
            catch (InvalidMoveException e)
            {
                error = e.Reason;
                return null;
            }
            return TryApply(moveObj, out error);
        }

        public State? TryApply(Move move, out InvalidMoveReason error)
        {
            var newInner = _inner.Copy();
            if (!newInner.TryApply(move, out error))
            {
                return null;
            }
            return new State(newInner);
        }

        public override bool Equals(object obj) => Equals(obj as State);

        public bool Equals([AllowNull] State other)
        {
            if (other == null) return false;

            // todo: board no longer implements iequatable
            if (!Board.Equals(other.Board) ||
                ActiveSide != other.ActiveSide ||
                CastlingRights != other.CastlingRights ||
                EnPassantTarget != other.EnPassantTarget ||
                HalfMoveClock != other.HalfMoveClock ||
                FullMoveNumber != other.FullMoveNumber)
            {
                return false;
            }

            Debug.Assert(Hash == other.Hash);
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

        public override string ToString() => _inner.ToString();

        public IEnumerable<Move> GetMoves() => GetSuccessors().Select(p => p.Move);

        public IEnumerable<SuccessorPair> GetSuccessors()
        {
            foreach (var move in _inner.GetPseudoLegalMoves())
            {
                var newInner = _inner.Copy();
                if (newInner.TryApply(move, out _))
                {
                    var succ = new State(newInner);
                    yield return (move, succ);
                }
            }
        }

        public IEnumerable<Tile> GetOccupiedTiles() => GetTiles().Where(t => t.HasPiece);

        public IEnumerable<Tile> GetTiles()
        {
            for (var file = FileA; file <= FileH; file++)
            {
                for (var rank = Rank1; rank <= Rank8; rank++)
                {
                    var location = new Location(file, rank);
                    yield return new Tile(location, _inner.Board[location]);
                }
            }
        }

        // todo: remove this from public api?
        public State SetActiveSide(Side value)
        {
            var newInner = _inner.Copy();
            newInner.ActiveSide = value;
            newInner.Hash ^= ZobristKey.ForActiveSide(ActiveSide);
            newInner.Hash ^= ZobristKey.ForActiveSide(value);
            return new State(newInner);
        }
    }
}
