using ChessBot.Types;

namespace ChessBot
{
    public struct TilesEnumerator
    {
        private readonly TileList _tiles;
        private int _location;

        internal TilesEnumerator(TileList tiles)
        {
            _tiles = tiles;
            _location = -1;
        }

        public Tile Current => _tiles[new Location((byte)_location)];

        public TilesEnumerator GetEnumerator() => this;

        public bool MoveNext() => ++_location < 64;
    }
}
