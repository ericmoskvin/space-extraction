namespace SpaceExtraction.Match;

/// <summary>
/// High-level lifecycle phases for a match session.
/// </summary>
public enum MatchPhase
{
    WaitingForPlayers = 0,
    InProgress = 1,
    Ended = 2
}
