namespace PoolScoreTracker.Domain;

/// <summary>
/// How the evening is running: the set on now, the average set so far, and
/// when the last set should finish if the rest go like the ones already
/// played. Anything the app cannot know yet is null rather than a guess.
/// </summary>
public sealed record Timing(TimeSpan? Current, TimeSpan? Average, DateTimeOffset? Finish, int SetsLeft)
{
    public static readonly Timing None = new(null, null, null, 0);

    public static TimeSpan? Between(DateTimeOffset? started, DateTimeOffset? finished, DateTimeOffset now)
    {
        if (started is not { } from) return null;
        var length = (finished ?? now) - from;
        return length < TimeSpan.Zero ? TimeSpan.Zero : length;
    }

    /// <summary>12:34, or 1:02:15 once a set runs past the hour.</summary>
    public static string Clock(TimeSpan length) =>
        length.TotalHours >= 1
            ? $"{(int)length.TotalHours}:{length.Minutes:00}:{length.Seconds:00}"
            : $"{length.Minutes}:{length.Seconds:00}";

    /// <summary>A time of day, the way the PC's clock shows it.</summary>
    public static string TimeOfDay(DateTimeOffset at) => at.ToLocalTime().ToString("t");
}
