namespace PoolScoreTracker.Domain;

/// <summary>
/// House rules for who breaks. With alternate break the app can follow the
/// break rack by rack. With winner breaks it cannot - only totals are recorded,
/// so it shows nothing rather than guessing.
/// </summary>
public static class Breaks
{
    /// <summary>
    /// Which side breaks the first rack of a set: home, unless that set's break
    /// was swapped by hand.
    /// </summary>
    public static Side FirstBreakSide(bool breakSwapped) => breakSwapped ? Side.Away : Side.Home;

    /// <summary>
    /// Who breaks the rack about to be played, or null when there is nothing to
    /// show. Alternate break hands it over after every rack, so the racks
    /// already played decide it. <paramref name="decided"/> means the race is
    /// won and there is no next rack.
    /// </summary>
    public static Side? RackBreakSide(Side first, int racksPlayed, bool decided, BreakRule rule)
    {
        if (rule != BreakRule.Alternate || decided) return null;
        return racksPlayed % 2 == 0 ? first : first.Opposite();
    }
}
