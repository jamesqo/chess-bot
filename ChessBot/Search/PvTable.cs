using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ChessBot.Search
{
    /// <summary>
    /// Stores the most recent PV for each depth level during a search.
    /// </summary>
    /// <remarks>
    /// Internally, this class uses a triangular array with d moves stored for depth = d.
    /// If the PV is cut off early (ie. due to mate, or a TT hit meaning we only know the next best move),
    /// then we "null-terminate" it with zero-valued moves.
    /// </remarks>
    internal class PvTable
    {
        private readonly Move[] _buffer;
        private readonly int _maxDepth;

        public PvTable(int maxDepth)
        {
            int bufferSize = (maxDepth * maxDepth + maxDepth) / 2;
            _buffer = new Move[bufferSize];
            _maxDepth = maxDepth;
        }

        // copies the pv from (depth - 1) to depth along with the given first move
        public void BubbleUp(int depth, Move firstMove)
        {
            Debug.Assert(depth > 0 && depth <= _maxDepth);
            Debug.Assert(firstMove.IsValid);

            int thisIndex = GetIndex(depth);
            _buffer[thisIndex] = firstMove;

            if (depth > 1)
            {
                int childIndex = GetIndex(depth - 1);
                Array.Copy(_buffer, childIndex, _buffer, thisIndex + 1, depth - 1);
            }
        }

        // indicates there is no PV for the given depth (eg. because of mate)
        public void SetNoPv(int depth)
        {
            Array.Clear(_buffer, GetIndex(depth), depth);
        }

        // indicates we only know the next best move for the given depth (eg. TT hit)
        public void SetOneMovePv(int depth, Move onlyMove)
        {
            Debug.Assert(onlyMove.IsValid);

            int index = GetIndex(depth);
            _buffer[index] = onlyMove;
            if (depth > 1) Array.Clear(_buffer, index + 1, depth - 1);
        }

        public Span<Move> GetTop(bool excludeZeros = true) => GetPv(_maxDepth, excludeZeros);

        private Span<Move> GetPv(int depth, bool excludeZeros)
        {
            var result = _buffer.AsSpan(GetIndex(depth), depth);
            if (excludeZeros)
            {
                int count = result.IndexOf(default(Move));
                if (count != -1) result = result.Slice(0, count);
            }
            return result;
        }

        private int GetIndex(int depth)
        {
            Debug.Assert(depth > 0 && depth <= _maxDepth);

            depth--;
            return (depth * depth + depth) / 2;
        }
    }
}
