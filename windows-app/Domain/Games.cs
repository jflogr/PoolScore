namespace PoolScoreTracker.Domain;

/// <summary>One game won: by whom, and when it started and finished.</summary>
public sealed record GameTime(Side Winner, DateTimeOffset? StartedAt, DateTimeOffset FinishedAt)
{
    /// <summary>Null for a game nobody saw start - the first one of the night.</summary>
    public TimeSpan? Duration => Timing.Between(StartedAt, FinishedAt, FinishedAt);
}

/// <summary>
/// The games of one set or one race, in the order they were won, and when the
/// one on the table now began. Not meant to be exact: a game is timed from
/// one score change to the next, so it includes racking up and any pause.
/// </summary>
public sealed class GameLog
{
    public List<GameTime> Played { get; } = [];

    /// <summary>When the game on the table began, if anyone knows.</summary>
    public DateTimeOffset? CurrentStartedAt { get; set; }

    public TimeSpan? Current(DateTimeOffset now) => Timing.Between(CurrentStartedAt, null, now);

    /// <summary>
    /// The match's playing time: its games added up, plus the one on the table.
    /// Null until a game has a time or one is being timed.
    /// </summary>
    public TimeSpan? Total(DateTimeOffset now)
    {
        var played = Sum(Played);
        var current = Current(now);
        return played is null && current is null ? null : (played ?? TimeSpan.Zero) + (current ?? TimeSpan.Zero);
    }

    /// <summary>The games' times added up; null if none has a time. An untimed game adds nothing.</summary>
    public static TimeSpan? Sum(IEnumerable<GameTime> games)
    {
        var lengths = games.Select(game => game.Duration).OfType<TimeSpan>().ToList();
        return lengths.Count == 0 ? null : lengths.Aggregate(TimeSpan.Zero, (total, length) => total + length);
    }

    /// <summary>
    /// Games won closer together than this were not played that fast - the
    /// score is being caught up after it was forgotten.
    /// </summary>
    public static readonly TimeSpan CatchUp = TimeSpan.FromSeconds(5);

    // The run of games being caught up just now: where it starts in Played,
    // and when its last game was scored. Only matters for a few seconds, so
    // it is not saved.
    private int _burstFrom = -1;
    private DateTimeOffset? _lastWonAt;

    /// <summary>
    /// A side won the game on the table; the next one starts now. Games scored
    /// within <see cref="CatchUp"/> of each other share the time since the
    /// first of them began, evenly - two forgotten games are not one of 15
    /// minutes and one of a second.
    /// </summary>
    public void Won(Side side, DateTimeOffset now)
    {
        var catchingUp = _lastWonAt is { } previous && now - previous < CatchUp
                         && _burstFrom >= 0 && _burstFrom < Played.Count;

        Played.Add(new GameTime(side, CurrentStartedAt, now));
        CurrentStartedAt = now;
        _lastWonAt = now;

        if (catchingUp) Spread(_burstFrom, now);
        else _burstFrom = Played.Count - 1;
    }

    /// <summary>
    /// A rack taken off a side removes the last game it won. If that was the
    /// game just finished, it is still being played, so its clock resumes from
    /// when it really began - a wrong tap put right straight away costs nothing.
    /// Inside a run being caught up, the games left share the run's time again,
    /// and the game on the table starts from when the run was scored.
    /// </summary>
    public void TakeBack(Side side)
    {
        var index = Played.FindLastIndex(game => game.Winner == side);
        if (index < 0) return;

        var game = Played[index];
        var inRun = _burstFrom >= 0 && _burstFrom < index && index == Played.Count - 1;
        Played.RemoveAt(index);

        if (inRun)
        {
            Spread(_burstFrom, game.FinishedAt);
            CurrentStartedAt = game.FinishedAt;
        }
        else if (index == Played.Count)
        {
            CurrentStartedAt = game.StartedAt;
            if (_burstFrom >= Played.Count) _burstFrom = -1;
        }
    }

    /// <summary>
    /// Shares the time from the first game of the run starting to <paramref name="end"/>
    /// evenly between the run's games, in order. A run whose first game has
    /// no start - the first game of the night - has nothing to share.
    /// </summary>
    private void Spread(int from, DateTimeOffset end)
    {
        if (Played[from].StartedAt is not { } start || end <= start) return;

        var count = Played.Count - from;
        var share = (end - start) / count;

        for (var i = 0; i < count; i++)
        {
            Played[from + i] = Played[from + i] with
            {
                StartedAt = start + share * i,
                FinishedAt = i == count - 1 ? end : start + share * (i + 1)
            };
        }
    }

    /// <summary>Drops a side's latest games until it has no more than its score - after a shorter race is set.</summary>
    public void Trim(Side side, int score)
    {
        while (Played.Count(game => game.Winner == side) > score) TakeBack(side);
    }

    public void Clear()
    {
        Played.Clear();
        CurrentStartedAt = null;
        _burstFrom = -1;
        _lastWonAt = null;
    }
}

/// <summary>Fastest, slowest and average of the games that have a time.</summary>
public sealed record GameSummary(int Timed, TimeSpan? Fastest, TimeSpan? Slowest, TimeSpan? Average)
{
    public static GameSummary Of(IEnumerable<GameTime> games)
    {
        var lengths = games.Select(game => game.Duration).OfType<TimeSpan>().ToList();
        if (lengths.Count == 0) return new GameSummary(0, null, null, null);

        return new GameSummary(
            lengths.Count,
            lengths.Min(),
            lengths.Max(),
            TimeSpan.FromTicks((long)lengths.Average(length => length.Ticks)));
    }

    /// <summary>One line for the top of a list of matches.</summary>
    public string Describe() =>
        Timed == 0
            ? "Games: none timed yet"
            : $"Games: fastest {Timing.Clock(Fastest!.Value)}    ·    slowest {Timing.Clock(Slowest!.Value)}    ·    "
              + $"average {Timing.Clock(Average!.Value)}    ·    {Timed} timed";
}
