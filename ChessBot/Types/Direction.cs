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
}
