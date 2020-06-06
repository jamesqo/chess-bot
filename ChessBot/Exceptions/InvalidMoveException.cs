using System;

namespace ChessBot.Exceptions
{
    /// <summary>
    /// Exception thrown when an attempt to make an invalid chess move is made.
    /// </summary>
    [Serializable]
    public class InvalidMoveException : Exception
    {
        public InvalidMoveException(InvalidMoveReason reason) : base(GetMessage(reason))
        {
            Reason = reason;
        }

        public InvalidMoveReason Reason { get; }

        private static string GetMessage(InvalidMoveReason reason) => reason switch
        {
            InvalidMoveReason.CouldNotFindKing => "Could not find king while parsing castling move",
            InvalidMoveReason.BadCaptureNotation => "Incorrect capture notation while parsing move",
            InvalidMoveReason.CouldNotInferSource => "Could not infer source tile while parsing move",
            InvalidMoveReason.SameSourceAndDestination => "Source and destination tiles are the same",
            InvalidMoveReason.EmptySource => "Source tile is empty",
            InvalidMoveReason.MismatchedSourcePiece => "Piece's color does not match active player's color",
            InvalidMoveReason.DestinationOccupiedByFriendlyPiece => "Destination tile is already occupied by a piece of the same color",
            InvalidMoveReason.BadPromotionKind => "A promotion happens iff a pawn moves to the back rank",
            InvalidMoveReason.ViolatesMovementRules => "Move violates movement rules",
            InvalidMoveReason.AllowsKingToBeAttacked => "Move allows the active side's king to be attacked",
        };
    }
}
