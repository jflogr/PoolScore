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
    /// When the set got going: its Start, or its first rack if nobody pressed
    /// Start. Kept for the changeover between sets; the set's own time is its
    /// games added up.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>The set's games in order, and the clock on the one being played.</summary>
    public GameLog Games { get; } = new();

    /// <summary>Who is at the table in this set.</summary>
    public ShootingOrder Turns { get; } = new();

    public int RacksPlayed => Home + Away;

    /// <summary>The set's playing time so far: its games added up, plus the one on the table.</summary>
    public TimeSpan? Duration(DateTimeOffset now) => Games.Total(now);
}
