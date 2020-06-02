using ChessBot.Types;

namespace ChessBot
{
    public struct OccupiedTilesEnumerator
    {
        // don't mark this as readonly since it's a mutable struct
        private TilesEnumerator _inner;

        internal OccupiedTilesEnumerator(TileList tiles)
        {
            _inner = new TilesEnumerator(tiles);
        }

        public Tile Current => _inner.Current;

        public OccupiedTilesEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            do
            {
                if (!_inner.MoveNext())
                {
                    return false;
                }
            }
            while (!Current.HasPiece);
            return true;
        }
    }
}
