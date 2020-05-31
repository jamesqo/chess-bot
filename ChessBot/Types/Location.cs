using ChessBot.Exceptions;
using System;
using System.Runtime.CompilerServices;

namespace ChessBot.Types
{
    public struct Location : IEquatable<Location>
    {
        public static implicit operator Location((File, Rank) tuple)
            => new Location(tuple.Item1, tuple.Item2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Location left, Location right)
            => (left.File == right.File && left.Rank == right.Rank);

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

            File = file;
            Rank = rank;
        }

        public File File { get; }
        public Rank Rank { get; }

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

        public bool Equals(Location other) => this == other;

        public override int GetHashCode() => HashCode.Combine(File, Rank);

        public override string ToString() => $"{File.ToChar()}{Rank.ToChar()}";
    }
}
