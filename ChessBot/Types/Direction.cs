namespace ChessBot.Types
{
    /// <summary>
    /// Represents a direction on the chess board.
    /// </summary>
    internal enum Direction
    {
        North,
        East,
        South,
        West,
        Northeast,
        Southeast,
        Southwest,
        Northwest,

        Start = North,
        End = Northwest
    }

    internal static class DirectionHelpers
    {
        public static bool IsValid(this Direction direction)
            => direction >= Direction.Start && direction <= Direction.End;
    }
}
