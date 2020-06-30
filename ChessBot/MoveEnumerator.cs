using ChessBot.Helpers;
using ChessBot.Search;
using ChessBot.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using static ChessBot.StaticInfo;
using Bitboards = ChessBot.MutState.Bitboards;

namespace ChessBot
{
    /// <summary>
    /// Enumerates over the pseudo-legal moves in the current position.
    /// </summary>
    public struct MoveEnumerator : IEnumerable<Move>, IEnumerator<Move>
    {
        private enum Phase
        {
            Start,
            LoadCaptures,
            YieldCaptures,
            YieldKillers,
            LoadNonCaptures,
            YieldNonCaptures,
            End
        }

        private readonly Board _board;
        private readonly Bitboards _bbs;
        private readonly Side _activeSide;
        private readonly Location? _enPassantTarget;
        private readonly bool _canReallyCastleKingside;
        private readonly bool _canReallyCastleQueenside;
        private readonly MoveFlags _flags;
        private readonly Killers _killers;

        private Move _current;
        private PooledList<Move> _buffer;
        private int _index;
        private Phase _phase;

        internal MoveEnumerator(MutState state, MoveFlags flags, Killers killers)
        {
            Debug.Assert((flags & MoveFlags.NonCaptures) != 0 || killers.Count == 0);

            _board = state.Board;
            _bbs = state._bbs;
            _activeSide = state.ActiveSide;
            _enPassantTarget = state.EnPassantTarget;
            _canReallyCastleKingside = state.CanReallyCastleKingside;
            _canReallyCastleQueenside = state.CanReallyCastleQueenside;
            _flags = flags;
            _killers = killers;

            _current = default;
            _buffer = default;
            _index = 0;
            _phase = Phase.Start;
        }

        public readonly Move Current => _current;
        public readonly MoveEnumerator GetEnumerator() => this;

        IEnumerator<Move> IEnumerable<Move>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        object IEnumerator.Current => Current;
        void IEnumerator.Reset() => throw new NotSupportedException();

        // We attempt to return "better" moves (ie. ones that are more likely to cause cutoffs) first.
        // Captures are returned first, then killer moves, then non-captures.
        // Captures are ordered according to MVV-LVA. In the future, non-captures will be ordered according to the history heuristic.
        public bool MoveNext()
        {
            switch (_phase)
            {
                case Phase.Start:
                    if ((_flags & MoveFlags.Captures) != 0)
                    {
                        _phase = Phase.LoadCaptures;
                        goto case Phase.LoadCaptures;
                    }
                    else
                    {
                        _phase = Phase.YieldKillers;
                        goto case Phase.YieldKillers;
                    }
                case Phase.LoadCaptures:
                    _buffer = LoadCaptures();
                    _phase = Phase.YieldCaptures;
                    goto case Phase.YieldCaptures;
                case Phase.YieldCaptures:
                    if (_index == _buffer.Count)
                    {
                        _buffer.Dispose();
                        _index = 0;
                        if ((_flags & MoveFlags.NonCaptures) != 0)
                        {
                            _phase = Phase.YieldKillers;
                            goto case Phase.YieldKillers;
                        }
                        else
                        {
                            _phase = Phase.End;
                            goto case Phase.End;
                        }
                    }
                    _current = _buffer[_index++];
                    return true;
                case Phase.YieldKillers:
                    while (_index < _killers.Count && !IsKillerOk(_killers[_index]))
                        _index++;
                    if (_index == _killers.Count)
                    {
                        _index = 0;
                        _phase = Phase.LoadNonCaptures;
                        goto case Phase.LoadNonCaptures;
                    }
                    _current = _killers[_index++];
                    return true;
                case Phase.LoadNonCaptures:
                    _buffer = LoadNonCaptures();
                    _phase = Phase.YieldNonCaptures;
                    goto case Phase.YieldNonCaptures;
                case Phase.YieldNonCaptures:
                    while (_index < _buffer.Count && !IsNonCaptureOk(_buffer[_index]))
                        _index++;
                    if (_index == _buffer.Count)
                    {
                        _buffer.Dispose();
                        _index = 0;
                        _phase = Phase.End;
                        goto case Phase.End;
                    }
                    _current = _buffer[_index++];
                    return true;
                case Phase.End:
                default:
                    return false;
            }
        }

        public void Dispose()
        {
            if (!_buffer.WasDisposed) _buffer.Dispose();
        }

        private readonly unsafe PooledList<Move> LoadCaptures()
        {
            var opposingSide = _activeSide.Flip();
            Bitboard occupied = _bbs.Occupies[0] | _bbs.Occupies[1];

            var result = PooledList<Move>.Get(16);

            // Loop over all pieces that can be captured, from most to least valuable
            for (var victimKind = PieceKind.Queen; victimKind >= PieceKind.Pawn; victimKind--)
            {
                var victim = new Piece(opposingSide, victimKind);
                for (Bitboard ds = _bbs.PiecePlacement[victim.ToIndex()]; !ds.IsZero; ds = ds.ClearNext())
                {
                    var destination = ds.NextLocation();
                    Debug.Assert(_board[destination].HasPiece && _board[destination].Piece == victim);

                    // Loop over all pieces that can capture at `destination`, from least to most valuable
                    Bitboard activeAttacks = _bbs.Attacks[(int)_activeSide];
                    if (activeAttacks[destination])
                    {
                        for (var aggKind = PieceKind.Pawn; aggKind <= PieceKind.King; aggKind++)
                        {
                            var agg = new Piece(_activeSide, aggKind);
                            for (Bitboard ss = _bbs.PiecePlacement[agg.ToIndex()]; !ss.IsZero; ss = ss.ClearNext())
                            {
                                var source = ss.NextLocation();
                                Debug.Assert(_board[source].HasPiece && _board[source].Piece == agg);

                                var sourceAttacks = GetAttackBitboard(_board[source].Piece, source, occupied);
                                if (sourceAttacks[destination])
                                {
                                    bool isPromotion = (aggKind == PieceKind.Pawn && source.Rank == SeventhRank(_activeSide));
                                    if (isPromotion)
                                    {
                                        result.Add(new Move(source, destination, promotionKind: PieceKind.Queen));
                                        result.Add(new Move(source, destination, promotionKind: PieceKind.Knight));
                                        result.Add(new Move(source, destination, promotionKind: PieceKind.Rook));
                                        result.Add(new Move(source, destination, promotionKind: PieceKind.Bishop));
                                    }
                                    else
                                    {
                                        result.Add(new Move(source, destination));
                                    }
                                }
                            }
                        }
                    }

                    // En passant captures need to be handled specially
                    if (_enPassantTarget is Location epTarget)
                    {
                        bool canBeCapturedEp = (destination == epTarget.Up(ForwardStep(opposingSide)));
                        if (canBeCapturedEp)
                        {
                            Debug.Assert(victimKind == PieceKind.Pawn);

                            Bitboard pawnPlacement = _bbs.PiecePlacement[new Piece(_activeSide, PieceKind.Pawn).ToIndex()];
                            if (destination.File > File.FileA && pawnPlacement[destination.Left(1)])
                            {
                                result.Add(new Move(destination.Left(1), epTarget));
                            }
                            if (destination.File < File.FileH && pawnPlacement[destination.Right(1)])
                            {
                                result.Add(new Move(destination.Right(1), epTarget));
                            }
                        }
                    }
                }
            }

            return result;
        }

        private readonly unsafe PooledList<Move> LoadNonCaptures()
        {
            var result = PooledList<Move>.Get(32);

            for (Bitboard ss = _bbs.Occupies[(int)_activeSide]; !ss.IsZero; ss = ss.ClearNext())
            {
                var source = ss.NextLocation();
                var piece = _board[source].Piece;

                for (Bitboard ds = GetNonCaptureDestinations(source); !ds.IsZero; ds = ds.ClearNext())
                {
                    var destination = ds.NextLocation();
                    bool isPromotion = (piece.Kind == PieceKind.Pawn && source.Rank == SeventhRank(_activeSide));

                    if (isPromotion)
                    {
                        result.Add(new Move(source, destination, promotionKind: PieceKind.Queen));
                        result.Add(new Move(source, destination, promotionKind: PieceKind.Knight));
                        result.Add(new Move(source, destination, promotionKind: PieceKind.Rook));
                        result.Add(new Move(source, destination, promotionKind: PieceKind.Bishop));
                    }
                    else
                    {
                        result.Add(new Move(source, destination));
                    }
                }
            }

            return result;
        }

        private readonly bool IsKillerOk(Move killer)
        {
            var source = killer.Source;
            if (!_board[source].HasPiece || _board[source].Piece.Side != _activeSide)
            {
                return false;
            }

            // Ensure we haven't already returned this move in case it happens to be a capture in this state
            bool isPseudoLegalAndNonCapture = GetNonCaptureDestinations(source)[killer.Destination];
            return isPseudoLegalAndNonCapture;
        }

        private readonly bool IsNonCaptureOk(Move nonCapture) => !_killers.Contains(nonCapture);

        private readonly unsafe Bitboard GetNonCaptureDestinations(Location source)
        {
            Debug.Assert(_board[source].HasPiece);
            Debug.Assert(_board[source].Piece.Side == _activeSide);

            var result = Bitboard.Zero;

            var piece = _board[source].Piece;
            var (side, kind) = (piece.Side, piece.Kind);
            Bitboard occupied = _bbs.Occupies[0] | _bbs.Occupies[1];
            Debug.Assert(side == _activeSide); // this assumption is only used when we check for castling availability

            // don't bother with attack vectors for pawns. they're totally unrelated to where the pawn can move without capturing.
            if (kind == PieceKind.Pawn)
            {
                Debug.Assert(source.Rank != EighthRank(side)); // pawns should be promoted once they reach the eighth rank, so Up() should be safe

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
                return result;
            }

            result = GetAttackBitboard(piece, source, occupied);
            // we can't move to squares occupied by our own pieces, so exclude those.
            // since we already dealt with captures, exclude squares that are occupied by opposing pieces.
            result &= ~occupied;
            if (kind == PieceKind.King)
            {
                if (_canReallyCastleKingside) result |= source.Right(2).GetMask();
                if (_canReallyCastleQueenside) result |= source.Left(2).GetMask();
            }

            return result;
        }
    }
}
