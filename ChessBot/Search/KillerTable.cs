using ChessBot.Types;
using System.Diagnostics;

namespace ChessBot.Search
{
    internal class KillerTable
    {
        private readonly Killers[] _buffer;

        public KillerTable(int maxDepth)
        {
            Debug.Assert(maxDepth > 0);

            _buffer = new Killers[maxDepth];
        }

        public void Add(int depth, Move move)
        {
            _buffer[depth - 1] = _buffer[depth - 1].Add(move);
        }

        public void Clear(int depth)
        {
            _buffer[depth - 1] = Killers.Empty;
        }

        public Killers this[int depth] => _buffer[depth - 1];
    }
}
