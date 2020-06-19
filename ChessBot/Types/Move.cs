using System;
using ChessBot.Exceptions;
using ChessBot.Helpers;

namespace ChessBot.Types
{
    /// <summary>
    /// Stores information about a chess move.
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        /// <summary>
        /// Parses a <see cref="Move"/> from algebraic notation and a contextual <see cref="State"/> object.
        /// </summary>
        /// <remarks>
        /// For the most part, this method doesn't actually check whether the move is valid; that's done in <see cref="State.Apply(Move)"/>.
        /// </remarks>
        public static Move Parse(string an, State context) => new AnParser(an).Parse(context);

        public static Move ParseLong(string lan) => new AnParser(lan).ParseLong();

        public static bool operator ==(Move left, Move right) => left.Equals(right);
        public static bool operator !=(Move left, Move right) => !(left == right);

        public Move(Location source, Location destination, PieceKind? promotionKind = null)
        {
            if (source == destination)
            {
                throw new InvalidMoveException(InvalidMoveReason.SameSourceAndDestination);
            }

            switch (promotionKind)
            {
                case null:
                case PieceKind.Bishop:
                case PieceKind.Knight:
                case PieceKind.Queen:
                case PieceKind.Rook:
                    break;
                default:
                    throw new InvalidMoveException(InvalidMoveReason.BadPromotionKind);
            }

            int kindValue = promotionKind.HasValue ? ((int)promotionKind.Value + 1) : 0;
            _value = (ushort)(source.Value | (destination.Value << DestinationShift) | (kindValue << PromotionKindShift));
        }

        private readonly ushort _value;

        private const ushort SourceMask        = 0b0000_0000_0011_1111;
        private const ushort DestinationMask   = 0b0000_1111_1100_0000;
        private const ushort PromotionKindMask = 0b0111_0000_0000_0000;

        private const int DestinationShift = Location.NumberOfBits;
        private const int PromotionKindShift = Location.NumberOfBits * 2;

        public Location Source => new Location((byte)(_value & SourceMask));
        public Location Destination => new Location((byte)((_value & DestinationMask) >> DestinationShift));
        public PieceKind? PromotionKind
        {
            get
            {
                int kindValue = (_value & PromotionKindMask) >> PromotionKindShift;
                return kindValue == 0 ? (PieceKind?)null : (PieceKind)(kindValue - 1);
            }
        }

        internal bool IsDefault => _value == 0;
        internal bool IsValid => (Source.IsValid && Destination.IsValid && (PromotionKind?.IsValid() ?? true));

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public bool Equals(Move other) => _value == other._value;

        public override int GetHashCode() => _value;

        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire();
            sb.Append(Source);
            sb.Append(Destination);
            sb.Append(PromotionKind switch
            {
                PieceKind.Knight => "N",
                PieceKind.Bishop => "B",
                PieceKind.Rook => "R",
                PieceKind.Queen => "Q",
                null => ""
            });
            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
