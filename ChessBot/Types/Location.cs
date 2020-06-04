using ChessBot.Exceptions;
using System;
using System.Diagnostics;

namespace ChessBot.Types
{
    /// <summary>
    /// Represents a location on the chess board, such as a1. Does not store info about the <see cref="Piece"/> at that location (if any).
    /// </summary>
    public readonly struct Location : IEquatable<Location>
    {
        private readonly byte _value;

        internal const int NumberOfBits = 6;
        internal const int NumberOfValues = Board.NumberOfTiles;

        private const byte FileMask = 0b0000_0111;
        private const byte RankMask = 0b0011_1000;
        private const int RankShift = 3;

        public static implicit operator Location((File, Rank) tuple)
            => new Location(tuple.Item1, tuple.Item2);

        public static bool operator ==(Location left, Location right) => left.Equals(right);
        public static bool operator !=(Location left, Location right) => !(left == right);

        public static Location FromIndex(int value)
        {
            if (value < 0 || value >= NumberOfValues) throw new ArgumentOutOfRangeException(nameof(value));
            return new Location((byte)value);
        }

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
            Debug.Assert(file.IsValid());
            Debug.Assert(rank.IsValid());

            _value = (byte)((int)file | ((int)rank << RankShift));
        }

        internal Location(byte value)
        {
            _value = value;
            Debug.Assert(IsValid);
        }

        public File File => (File)(_value & FileMask);
        public Rank Rank => (Rank)((_value & RankMask) >> RankShift);

        internal byte Value => _value;
        internal bool IsValid => File.IsValid() && Rank.IsValid();

        public void Deconstruct(out File file, out Rank rank)
        {
            file = File;
            rank = Rank;
        }

        // used on perf-sensitive codepaths so we don't have to perform additional computation, ie. like Up().Right()
        public Location Add(int fileShift, int rankShift) => (File + fileShift, Rank + rankShift);

        public Location Up(int shift) => (File, Rank + shift);
        public Location Down(int shift) => Up(-shift);
        public Location Left(int shift) => Right(-shift);
        public Location Right(int shift) => (File + shift, Rank);

        public override bool Equals(object obj)
            => obj is Location other && Equals(other);

        public bool Equals(Location other) => _value == other._value;

        public override int GetHashCode() => _value;

        public Bitboard GetMask() => (1UL << _value);

        public int ToIndex() => _value;

        public override string ToString() => $"{File.ToChar()}{Rank.ToChar()}";
    }
}
