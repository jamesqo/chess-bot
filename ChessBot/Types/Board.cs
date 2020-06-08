using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

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

        private fixed byte _bytes[32];

        public unsafe PieceOrNone this[Location location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(location.IsValid);

                int index = location.Value;
                byte pieceValue = _bytes[index / 2];
                pieceValue >>= (index % 2) * 4;
                pieceValue &= 0b0000_1111;
                return new PieceOrNone(pieceValue);
            }
            set
            {
                Debug.Assert(location.IsValid);
                Debug.Assert(value.IsValid);

                int index = location.Value;
                byte pieceValue = value.Value; // 0 represents an empty tile
                ref byte target = ref _bytes[index / 2];
                if (index % 2 != 0)
                {
                    target &= 0b0000_1111;
                    target |= (byte)(pieceValue << 4);
                }
                else
                {
                    target &= 0b1111_0000;
                    target |= pieceValue;
                }
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
            for (int i = 0; i < sizeof(Board); i++)
            {
                if (_bytes[i] != other._bytes[i]) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            for (int i = 0; i < sizeof(Board); i++)
            {
                hc.Add(_bytes[i]);
            }
            return hc.ToHashCode();
        }
    }
}
