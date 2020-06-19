using ChessBot.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ChessBot.Search
{
    internal class PvTable
    {
        private readonly Move[] _buffer; // triangular array
        private readonly int _maxDepth;

        public PvTable(int maxDepth)
        {
            int bufferSize = (maxDepth * maxDepth + maxDepth) / 2;
            _buffer = new Move[bufferSize];
            _maxDepth = maxDepth;
        }

        // copies the pv from (depth-1) to depth, along with the given move
        public void BubbleUp(int depth, Move pvMove)
        {
            Debug.Assert(depth > 0 && depth <= _maxDepth);

            int thisIndex = GetIndex(depth);
            _buffer[thisIndex] = pvMove;

            if (depth > 1)
            {
                int childIndex = GetIndex(depth - 1);
                Array.Copy(_buffer, childIndex, _buffer, thisIndex + 1, depth - 1);
            }
        }

        public ImmutableArray<Move> GetTop()
        {
            return ImmutableArray.Create(_buffer, GetIndex(_maxDepth), _maxDepth);
        }

        private int GetIndex(int depth)
        {
            Debug.Assert(depth > 0 && depth <= _maxDepth);

            depth--;
            return (depth * depth + depth) / 2;
        }
    }
}
