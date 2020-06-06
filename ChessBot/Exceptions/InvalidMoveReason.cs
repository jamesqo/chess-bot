namespace ChessBot.Exceptions
{
    public enum InvalidMoveReason
    {
        None,
        CouldNotFindKing,
        BadCaptureNotation,
        CouldNotInferSource,
        SameSourceAndDestination,
        EmptySource,
        MismatchedSourcePiece,
        DestinationOccupiedByFriendlyPiece,
        BadPromotionKind,
        ViolatesMovementRules,
        AllowsKingToBeAttacked
    }
}
