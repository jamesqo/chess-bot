using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    public struct BoardLocation : IEquatable<BoardLocation>
    {
        public static implicit operator BoardLocation((int, int) tuple)
        {
            var (row, column) = tuple;
            return new BoardLocation(row, column);
        }

        public static bool operator ==(BoardLocation left, BoardLocation right)
            => (left.Row == right.Row && left.Column == right.Column);

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

            return (row, column);
        }

        public BoardLocation(int row, int column)
        {
            if (row < 0 || row >= 8)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            if (column < 0 || column >= 8)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            Row = row;
            Column = column;
        }

        public int Row { get; }
        public int Column { get; }

        public void Deconstruct(out int row, out int column)
        {
            row = Row;
            column = Column;
        }

        public BoardLocation Up(int count) => (Row + count, Column);
        public BoardLocation Down(int count) => Up(-count);
        public BoardLocation Left(int count) => Right(-count);
        public BoardLocation Right(int count) => (Row, Column + count);

        public override bool Equals(object obj)
            => obj is BoardLocation other && Equals(other);

        public bool Equals(BoardLocation other) => this == other;

        public override int GetHashCode() => HashCode.Combine(Row, Column);

        public override string ToString()
        {
            var file = (char)(Column + 'a');
            var rank = (char)(Row + '1');
            return $"{file}{rank}";
        }
    }
}
