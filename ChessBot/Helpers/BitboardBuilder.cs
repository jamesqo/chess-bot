using ChessBot.Types;

namespace ChessBot.Helpers
{
    // WARNING: this is a mutable struct. don't copy or its behavior may be unintuitive.
    internal struct BitboardBuilder
    {
        private Bitboard _value;

        public BitboardBuilder(Bitboard value) => _value = value;

        public Bitboard Bitboard => _value;

        public void Clear(Location location) => _value = _value.Clear(location);
        public void ClearRange(Bitboard bb) => _value &= ~bb;
        public void Set(Location location) => _value = _value.Set(location);
        public void SetRange(Bitboard bb) => _value |= bb;
    }
}
