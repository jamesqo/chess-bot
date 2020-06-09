namespace ChessBot.Exceptions
{
    public enum InvalidMoveReason
    {
        None,
        BadAlgebraicNotation,
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
