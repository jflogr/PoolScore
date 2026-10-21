namespace PoolScoreTracker.Domain;

public sealed class RosterPlayer(string id, string name)
{
    public string Id { get; } = id;
    public string Name { get; set; } = name;
}

/// <summary>
/// One set's running score. Racks are counted up as they are played; the set is
/// committed once the race is won, which is what the leaderboard counts.
/// </summary>
public sealed class SetScore(int set)
{
    public int Set { get; } = set;
    public int Home { get; set; }
    public int Away { get; set; }
    public bool Committed { get; set; }
    public Side? Winner { get; set; }

    /// <summary>The home side breaks unless this set's break has been swapped by hand.</summary>
    public bool BreakSwapped { get; set; }

    /// <summary>
    /// When the set got going: the moment the set before it was finished, or
    /// its first rack if nothing started it - the first set of the night.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int RacksPlayed => Home + Away;

    /// <summary>How long the set took, or has taken so far. Null until it starts.</summary>
    public TimeSpan? Duration(DateTimeOffset now) => Timing.Between(StartedAt, FinishedAt, now);
}
