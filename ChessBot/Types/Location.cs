using ChessBot.Exceptions;
using System;
using System.Diagnostics;

namespace ChessBot.Types
{
    public struct Location : IEquatable<Location>
    {
        private readonly byte _value;

        private const byte FileMask = 0b0000_0111;
        private const byte RankMask = 0b0011_1000;
        private const int RankShift = 3;

        public static implicit operator Location((File, Rank) tuple)
            => new Location(tuple.Item1, tuple.Item2);

        public static bool operator ==(Location left, Location right) => left.Equals(right);

        public static bool operator !=(Location left, Location right) => !(left == right);

        public static Location Parse(string an) => TryParse(an) ?? throw new AnParseException($"Unable to parse location from '{an}'");

        public static Location? TryParse(string an)
        {
            if (an?.Length != 2) return null;

            var (file, rank) = (FileHelpers.FromChar(an[0]), RankHelpers.FromChar(an[1]));
            if (!file.IsValid() || !rank.IsValid()) return null;

            return (file, rank);
        }

        public Location(File file, Rank rank)
        {
            if (!file.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(file));
            }
            if (!rank.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(rank));
            }

            _value = (byte)((int)file | ((int)rank << RankShift));
        }

        internal Location(byte value)
        {
            _value = value;

            Debug.Assert(File.IsValid());
            Debug.Assert(Rank.IsValid());
        }

        public File File => (File)(_value & FileMask);
        public Rank Rank => (Rank)((_value & RankMask) >> RankShift);

        internal byte Value => _value;

        public void Deconstruct(out File file, out Rank rank)
        {
            file = File;
            rank = Rank;
        }

        public Location Up(int count) => (File, Rank + count);
        public Location Down(int count) => Up(-count);
        public Location Left(int count) => Right(-count);
        public Location Right(int count) => (File + count, Rank);

        public override bool Equals(object obj)
            => obj is Location other && Equals(other);

        public bool Equals(Location other)
            => (File == other.File && Rank == other.Rank);

        public override int GetHashCode() => HashCode.Combine(File, Rank);

        public Bitboard GetMask() => (1UL << _value);

        public override string ToString() => $"{File.ToChar()}{Rank.ToChar()}";
    }
}
