using ChessBot.Types;
using System;

namespace ChessBot.Types
{
    // todo: remove this?
    internal class PieceBitboards
    {
        private unsafe struct Buffer
        {
            public fixed ulong Bitboards[6];
        }

        public static readonly PieceBitboards Empty = new PieceBitboards(0UL, 0UL, 0UL, 0UL, 0UL, 0UL);

        private unsafe PieceBitboards(
            Bitboard pawns,
            Bitboard knights,
            Bitboard bishops,
            Bitboard rooks,
            Bitboard queens,
            Bitboard king)
        {
            _buffer.Bitboards[0] = pawns;
            _buffer.Bitboards[1] = knights;
            _buffer.Bitboards[2] = bishops;
            _buffer.Bitboards[3] = rooks;
            _buffer.Bitboards[4] = queens;
            _buffer.Bitboards[5] = king;
        }

        private PieceBitboards(in Buffer buffer)
        {
            _buffer = buffer;
        }

        private Buffer _buffer;

        public unsafe Bitboard Pawns => _buffer.Bitboards[0];
        public unsafe Bitboard Knights => _buffer.Bitboards[1];
        public unsafe Bitboard Bishops => _buffer.Bitboards[2];
        public unsafe Bitboard Rooks => _buffer.Bitboards[3];
        public unsafe Bitboard Queens => _buffer.Bitboards[4];
        public unsafe Bitboard King => _buffer.Bitboards[5];

        public unsafe Bitboard this[PieceKind kind]
        {
            get
            {
                if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
                return _buffer.Bitboards[(int)kind];
            }
        }

        internal static Builder CreateBuilder(PieceBitboards value = null) => new Builder(value ?? Empty);

        public struct Builder
        {
            private readonly PieceBitboards _value;

            internal Builder(PieceBitboards value) => _value = Copy(value);

            public PieceBitboards Value => _value;

            public unsafe Bitboard this[PieceKind kind]
            {
                get => _value[kind];
                set
                {
                    if (!kind.IsValid()) throw new ArgumentOutOfRangeException(nameof(kind));
                    _value._buffer.Bitboards[(int)kind] = value;
                }
            }

            private static PieceBitboards Copy(PieceBitboards value)
            {
                return new PieceBitboards(in value._buffer);
            }
        }
    }
}
