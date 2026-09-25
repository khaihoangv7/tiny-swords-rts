namespace TinyKingdom.AI
{
    public enum CommanderCommandResult
    {
        Accepted,
        RejectedInvalidActor,
        RejectedInvalidTarget,
        RejectedNotOwned,
        RejectedWrongUnitType,
        RejectedNotObserved,
        RejectedUnreachable,
        RejectedMatchNotPlaying
    }
}
