using ChessBot.Types;

namespace ChessBot.Helpers
{
    /// <summary>
    /// Mutable struct that helps with generating <see cref="Bitboard"/> values.
    /// </summary>
    /// <remarks>
    /// Since this is a mutable struct, try not to copy it or you may get unintuitive behavior.
    /// </remarks>
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
