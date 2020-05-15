using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ChessBot
{
    public class PlayerInfo : IEquatable<PlayerInfo>
    {
        public PlayerInfo(
            PlayerColor color,
            bool hasCastled = false,
            bool hasMovedKing = false,
            bool hasMovedKingsideRook = false,
            bool hasMovedQueensideRook = false)
        {
            Color = color;
            HasCastled = hasCastled;
            HasMovedKing = hasMovedKing;
            HasMovedKingsideRook = hasMovedKingsideRook;
            HasMovedQueensideRook = hasMovedQueensideRook;
        }

        public PlayerColor Color { get; }
        public bool HasCastled { get; }
        public bool HasMovedKing { get; }
        public bool HasMovedKingsideRook { get; }
        public bool HasMovedQueensideRook { get; }

        public override bool Equals(object obj) => Equals(obj as PlayerInfo);

        public bool Equals([AllowNull] PlayerInfo other)
        {
            if (other == null) return false;
            throw new NotImplementedException();
        }

        public override int GetHashCode() => throw new NotImplementedException();
    }
}
