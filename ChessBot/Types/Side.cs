namespace ChessBot.Types
{
    public enum Side
    {
        White,
        Black
    }

    public static class SideHelpers
    {
        public static bool IsValid(this Side side)
            => side >= Side.White && side <= Side.Black;

        public static bool IsWhite(this Side side)
            => side == Side.White;
    }
}
