namespace PoolScoreTracker.Domain;

/// <summary>
/// Turning roster names into what fits on a scoreboard. First names keep it
/// short; where two players in the same line-up share one, just enough of the
/// surname is added to tell them apart.
/// </summary>
public static class Names
{
    private sealed class Entry(string id, string raw)
    {
        public string Id { get; } = id;
        public string Raw { get; } = raw;
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
    }

    /// <summary>A 1 v 1 always has room for the whole name, whatever the setting.</summary>
    public static bool UseFullNames(ModeConfig config, NameStyle style) =>
        style == NameStyle.Full || (config.Kind == ModeKind.Match && config.TeamSize == 1);

    public static (string First, string Last) ParseName(string? raw)
    {
        var parts = (raw ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0
            ? (string.Empty, string.Empty)
            : (parts[0], string.Join(' ', parts.Skip(1)));
    }

    /// <summary>
    /// Maps each roster id in the line-up to the name to show. Ids that are not
    /// filled in are skipped rather than given a placeholder.
    /// </summary>
    public static Dictionary<string, string> BuildNameMap(
        IEnumerable<string?> lineup,
        bool useFull,
        Func<string, string?> rosterName)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        var entries = lineup
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => new Entry(id!, (rosterName(id!) ?? string.Empty).Trim()))
            .ToList();

        if (useFull)
        {
            foreach (var entry in entries) map[entry.Id] = entry.Raw;
            return map;
        }

        foreach (var entry in entries)
        {
            var (first, last) = ParseName(entry.Raw);
            entry.First = first;
            entry.Last = last;
        }

        // Group on the lower-cased first name, but show the name as it was typed.
        foreach (var group in entries.GroupBy(entry => entry.First.ToLowerInvariant()))
        {
            var members = group.ToList();

            if (members.Count == 1)
            {
                var only = members[0];
                map[only.Id] = only.First.Length > 0 ? only.First : only.Raw;
                continue;
            }

            foreach (var entry in members)
            {
                var prefix = UniqueLastPrefix(entry, members);
                map[entry.Id] = prefix.Length > 0
                    ? $"{entry.First} {prefix}"
                    : entry.First.Length > 0 ? entry.First : entry.Raw;
            }
        }

        return map;
    }

    /// <summary>
    /// Shortest leading slice of this entry's surname that nobody else sharing
    /// the first name starts with. Falls back to the whole surname - which is
    /// what happens when one surname is a prefix of another (Smith / Smithson).
    /// </summary>
    private static string UniqueLastPrefix(Entry entry, List<Entry> group)
    {
        var last = entry.Last;
        if (last.Length == 0) return string.Empty;

        var others = group.Where(other => !string.Equals(other.Id, entry.Id, StringComparison.Ordinal)).ToList();

        for (var length = 1; length <= last.Length; length++)
        {
            var prefix = last[..length].ToLowerInvariant();

            var collides = others.Any(other =>
                other.Last[..Math.Min(length, other.Last.Length)].ToLowerInvariant() == prefix);

            if (!collides) return last[..length];
        }

        return last;
    }

    public static string TeamName(IEnumerable<string> tokens, Func<string, string> nameForToken) =>
        string.Join(" / ", tokens.Select(nameForToken));

    public static string BannerTeamName(IEnumerable<string> tokens, Func<string, string> nameForToken) =>
        string.Join(" | ", tokens.Select(token => nameForToken(token).ToUpperInvariant()));
}
