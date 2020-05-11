using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot
{
    public struct BoardLocation
    {
        public static implicit operator BoardLocation((int, int) tuple)
        {
            var (row, column) = tuple;
            return new BoardLocation(row, column);
        }

        public static BoardLocation Parse(string algebraicNotation)
        {
            throw new NotImplementedException();
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

        public override string ToString()
        {
            var fileChar = (Column + 'a');
            var rankChar = (Row + 1);
            return $"{fileChar}{rankChar}";
        }
    }
}
