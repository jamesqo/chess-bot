using ChessBot.Types;
using System.Text;

namespace ChessBot.Console
{
    static class Helpers
    {
        public static string GetDisplayString(State state)
        {
            var sb = new StringBuilder();
            // todo: have whichever side the human is on at the bottom
            for (var rank = Rank.Rank8; rank >= Rank.Rank1; rank--)
            {
                for (var file = File.FileA; file <= File.FileH; file++)
                {
                    if (file > File.FileA) sb.Append(' ');
                    sb.Append(GetDisplayChar(state[file, rank]));
                }
                if (rank > Rank.Rank1) sb.AppendLine();
            }
            return sb.ToString();
        }

        static char GetDisplayChar(Tile tile)
            => tile.HasPiece ? tile.Piece.ToDisplayChar() : '.';
    }
}
