using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChessBot
{
    public struct BoardLocation : IEquatable<BoardLocation>
    {
        public static implicit operator BoardLocation((int, int) tuple)
        {
            var (column, row) = tuple;
            return new BoardLocation(column, row);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(BoardLocation left, BoardLocation right)
            => (left.Column == right.Column && left.Row == right.Row);

        public static bool operator !=(BoardLocation left, BoardLocation right) => !(left == right);

        public static BoardLocation Parse(string algebraicNotation)
        {
            if (algebraicNotation?.Length != 2)
            {
                throw new AlgebraicNotationParseException("Expected input of length 2");
            }

            int column = (algebraicNotation[0] - 'a');
            int row = (algebraicNotation[1] - '1');

            if ((column < 0 || column >= 8) || (row < 0 || row >= 8))
            {
                throw new AlgebraicNotationParseException("Invalid rank or file specified");
            }

            return (column, row);
        }

        public BoardLocation(int column, int row)
        {
            if (column < 0 || column >= 8)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
            if (row < 0 || row >= 8)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            Column = column;
            Row = row;
        }

        public int Column { get; }
        public int Row { get; }

        public void Deconstruct(out int column, out int row)
        {
            column = Column;
            row = Row;
        }

        public BoardLocation Up(int count) => (Column, Row + count);
        public BoardLocation Down(int count) => Up(-count);
        public BoardLocation Left(int count) => Right(-count);
        public BoardLocation Right(int count) => (Column + count, Row);

        public override bool Equals(object obj)
            => obj is BoardLocation other && Equals(other);

        public bool Equals(BoardLocation other) => this == other;

        public override int GetHashCode() => HashCode.Combine(Column, Row);

        public override string ToString()
        {
            var file = (char)(Column + 'a');
            var rank = (char)(Row + '1');
            return $"{file}{rank}";
        }
    }
}
