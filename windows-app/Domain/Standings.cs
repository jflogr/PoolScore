namespace PoolScoreTracker.Domain;

/// <summary>
/// One player's evening: sets won out of sets played, where they would finish
/// if they won every set they have left, and the racks behind those sets - a
/// hill-hill win and a sweep both count as one set, but not as the same night.
/// </summary>
public sealed record StandingRow(
    string Token, string Name, int Wins, int Played, int Total, int RacksWon, int RacksLost)
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
        var racksWon = new Dictionary<string, int>(StringComparer.Ordinal);
        var racksLost = new Dictionary<string, int>(StringComparer.Ordinal);

        var tokens = Enumerable.Range(1, config.Slots).Select(i => $"p{i}").ToList();
        foreach (var token in tokens)
        {
            wins[token] = 0;
            played[token] = 0;
            total[token] = 0;
            racksWon[token] = 0;
            racksLost[token] = 0;
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

            foreach (var token in match.Home)
            {
                racksWon[token] += score.Home;
                racksLost[token] += score.Away;
            }

            foreach (var token in match.Away)
            {
                racksWon[token] += score.Away;
                racksLost[token] += score.Home;
            }
        }

        return
        [
            .. tokens
                .Select(token => new StandingRow(
                    token, nameForToken(token), wins[token], played[token], total[token],
                    racksWon[token], racksLost[token]))
                // Wins, then sets played, then the rack difference - which is
                // what separates a night of sweeps from a night of hill-hills -
                // then name. The name comparison is culture-aware, so "adam"
                // sorts before "Bella" the way localeCompare does.
                .OrderByDescending(row => row.Wins)
                .ThenByDescending(row => row.Played)
                .ThenByDescending(row => row.RacksWon - row.RacksLost)
                .ThenBy(row => row.Name, StringComparer.InvariantCulture)
        ];
    }

    /// <summary>
    /// The same figures for singles and doubles, from the races played so
    /// far. There is no schedule, so nothing is left to play and Possible is
    /// just Wins - the window leaves that column out. Everyone in a doubles
    /// team is credited with the team's result, as in a round robin.
    /// </summary>
    public static IReadOnlyList<StandingRow> FromHistory(
        IReadOnlyList<PlayedMatch> history,
        Func<string, string?> nameForId)
    {
        var rows = new Dictionary<string, (string Name, int Wins, int Played, int Won, int Lost)>(StringComparer.Ordinal);

        void Credit(string?[] ids, string[] names, bool won, int racksFor, int racksAgainst)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] is not { } id) continue;

                (string Name, int Wins, int Played, int Won, int Lost) row = rows.TryGetValue(id, out var existing)
                    ? existing
                    : (nameForId(id) ?? names.ElementAtOrDefault(i) ?? id, 0, 0, 0, 0);

                rows[id] = (row.Name, row.Wins + (won ? 1 : 0), row.Played + 1, row.Won + racksFor, row.Lost + racksAgainst);
            }
        }

        foreach (var played in history)
        {
            Credit(played.HomeIds, played.HomeNames, played.Winner == Side.Home, played.Home, played.Away);
            Credit(played.AwayIds, played.AwayNames, played.Winner == Side.Away, played.Away, played.Home);
        }

        return
        [
            .. rows
                .Select(entry => new StandingRow(
                    entry.Key, entry.Value.Name, entry.Value.Wins, entry.Value.Played, entry.Value.Played,
                    entry.Value.Won, entry.Value.Lost))
                .OrderByDescending(row => row.Wins)
                .ThenByDescending(row => row.Played)
                .ThenByDescending(row => row.RacksWon - row.RacksLost)
                .ThenBy(row => row.Name, StringComparer.InvariantCulture)
        ];
    }
}
