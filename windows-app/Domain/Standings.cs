namespace PoolScoreTracker.Domain;

/// <summary>
/// Deliberately two numbers per player: sets won out of sets played, and where
/// they would finish if they won every set they have left.
/// </summary>
public sealed record StandingRow(string Token, string Name, int Wins, int Played, int Total)
{
    public int Possible => Wins + (Total - Played);
}

public static class Standings
{
    public static IReadOnlyList<StandingRow> Calculate(
        ModeConfig config,
        IReadOnlyList<SetScore> scores,
        Func<string, string> nameForToken)
    {
        var wins = new Dictionary<string, int>(StringComparer.Ordinal);
        var played = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = new Dictionary<string, int>(StringComparer.Ordinal);

        var tokens = Enumerable.Range(1, config.Slots).Select(i => $"p{i}").ToList();
        foreach (var token in tokens)
        {
            wins[token] = 0;
            played[token] = 0;
            total[token] = 0;
        }

        // Total counts every set the player is down to play, played or not.
        foreach (var match in config.Schedule)
        {
            foreach (var token in match.Home.Concat(match.Away)) total[token]++;
        }

        foreach (var score in scores)
        {
            if (!score.Committed) continue;

            var match = config.Schedule.FirstOrDefault(entry => entry.Set == score.Set);
            if (match is null) continue;

            foreach (var token in match.Home.Concat(match.Away)) played[token]++;

            var winners = score.Winner == Side.Home ? match.Home : match.Away;
            foreach (var token in winners) wins[token]++;
        }

        return
        [
            .. tokens
                .Select(token => new StandingRow(token, nameForToken(token), wins[token], played[token], total[token]))
                // Matches the page's sort: wins, then sets played, then name.
                // The name comparison is culture-aware, so "adam" sorts before
                // "Bella" the way localeCompare does - ordinal would not.
                .OrderByDescending(row => row.Wins)
                .ThenByDescending(row => row.Played)
                .ThenBy(row => row.Name, StringComparer.InvariantCulture)
        ];
    }
}
