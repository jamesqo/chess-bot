using ChessBot.Exceptions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ChessBot
{
    public struct Location : IEquatable<Location>
    {
        public static implicit operator Location((int, int) tuple)
        {
            var (column, row) = tuple;
            return new Location(column, row);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Location left, Location right)
            => (left.Column == right.Column && left.Row == right.Row);

        public static bool operator !=(Location left, Location right) => !(left == right);

        public static Location Parse(string an) => TryParse(an) ?? throw new AnParseException($"Unable to parse location from '{an}'");

        public static Location? TryParse(string an)
        {
            if (an?.Length != 2) return null;

            int column = (an[0] - 'a');
            int row = (an[1] - '1');

            if ((column < 0 || column >= 8) || (row < 0 || row >= 8)) return null;

            return (column, row);
        }

        public Location(int column, int row)
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

        public Location Up(int count) => (Column, Row + count);
        public Location Down(int count) => Up(-count);
        public Location Left(int count) => Right(-count);
        public Location Right(int count) => (Column + count, Row);

        public override bool Equals(object obj)
            => obj is Location other && Equals(other);

        public bool Equals(Location other) => this == other;

        public override int GetHashCode() => HashCode.Combine(Column, Row);

        public override string ToString()
        {
            var file = (char)(Column + 'a');
            var rank = (char)(Row + '1');
            return $"{file}{rank}";
        }
    }
}
