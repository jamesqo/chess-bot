using ChessBot.Types;
using System;

namespace ChessBot
{
    // todo: remove this?
    // todo: move to Types
    internal class PieceMasks
    {
        private unsafe struct Buffer
        {
            public fixed ulong Masks[6];
        }

        public static readonly PieceMasks Empty = new PieceMasks(0UL, 0UL, 0UL, 0UL, 0UL, 0UL);

        private unsafe PieceMasks(
            Bitboard pawns,
            Bitboard knights,
            Bitboard bishops,
            Bitboard rooks,
            Bitboard queens,
            Bitboard king)
        {
            _buffer.Masks[0] = pawns;
            _buffer.Masks[1] = knights;
            _buffer.Masks[2] = bishops;
            _buffer.Masks[3] = rooks;
            _buffer.Masks[4] = queens;
            _buffer.Masks[5] = king;
        }

        private PieceMasks(in Buffer buffer)
        {
            _buffer = buffer;
        }

        private Buffer _buffer;

        public unsafe Bitboard Pawns => _buffer.Masks[0];
        public unsafe Bitboard Knights => _buffer.Masks[1];
        public unsafe Bitboard Bishops => _buffer.Masks[2];
        public unsafe Bitboard Rooks => _buffer.Masks[3];
        public unsafe Bitboard Queens => _buffer.Masks[4];
        public unsafe Bitboard King => _buffer.Masks[5];

        public unsafe Bitboard this[PieceKind kind]
        {
            get
            {
                if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
                return _buffer.Masks[(int)kind];
            }
        }

        internal static Builder CreateBuilder(PieceMasks value = null) => new Builder(value ?? Empty);

        public struct Builder
        {
            private readonly PieceMasks _value;

            internal Builder(PieceMasks value) => _value = Copy(value);

            public PieceMasks Value => _value;

            public unsafe Bitboard this[PieceKind kind]
            {
                get => _value[kind];
                set
                {
                    if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
                    _value._buffer.Masks[(int)kind] = value;
                }
            }

            private static PieceMasks Copy(PieceMasks value)
            {
                return new PieceMasks(in value._buffer);
            }
        }
    }
}
