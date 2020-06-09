using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChessBot.Types
{
    /// <summary>
    /// Represents a list of tiles on a chess board.
    /// </summary>
    /// <remarks>
    /// Despite implementing <see cref="IEquatable{T}"/>, this is a **mutable** struct.
    /// Try to avoid copying this type where possible, as it's mutable and very large.
    /// </remarks>
    public unsafe struct Board : IEquatable<Board>
    {
        internal const int NumberOfTiles = 64;

        // we only need 32 bytes but this allows us to achieve faster indexing
        private fixed byte _bytes[NumberOfTiles];

        public unsafe PieceOrNone this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(location.IsValid);

                int index = location.Value;
                return new PieceOrNone(_bytes[index]);
            }
            set
            {
                Debug.Assert(location.IsValid);
                Debug.Assert(value.IsValid);

                int index = location.Value;
                _bytes[index] = value.Value;
            }
        }

        public IEnumerable<Tile> GetTiles()
        {
            for (var file = File.FileA; file <= File.FileH; file++)
            {
                for (var rank = Rank.Rank1; rank <= Rank.Rank8; rank++)
                {
                    var location = new Location(file, rank);
                    yield return new Tile(location, this[location]);
                }
            }
        }

        public IEnumerable<Tile> GetOccupiedTiles() => GetTiles().Where(t => t.HasPiece);

        public override bool Equals(object obj) => obj is Board other && Equals(other);

        public bool Equals(Board other)
        {
            for (int i = 0; i < NumberOfTiles; i++)
            {
                if (_bytes[i] != other._bytes[i]) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            for (int i = 0; i < NumberOfTiles; i++)
            {
                hc.Add(_bytes[i]);
            }
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb);
            return sb.ToString();
        }

        internal void ToString(StringBuilder sb)
        {
            for (var rank = Rank.Rank8; rank >= Rank.Rank1; rank--)
            {
                int gap = 0;
                for (var file = File.FileA; file <= File.FileH; file++)
                {
                    var piece = this[(file, rank)];
                    if (!piece.HasPiece) gap++;
                    else
                    {
                        if (gap > 0) sb.Append(gap);
                        sb.Append(piece.Piece.ToDisplayChar());
                        gap = 0;
                    }
                }
                if (gap > 0) sb.Append(gap);
                if (rank > Rank.Rank1) sb.Append('/');
            }
        }
    }
}
