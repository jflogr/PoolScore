using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Server;

public sealed record ScoreLine(
    string[] Home,
    string[] Away,
    int HomeScore,
    int AwayScore,
    int HomeTarget,
    int AwayTarget,
    string? Breaking);

public sealed record LeaderLine(string Name, int Wins, int Played, int Possible);

public sealed record UpcomingLine(
    int Set,
    int Round,
    string Home,
    string Away,
    string? Sitting,
    string Status,
    string? Score,
    string? BreaksFirst);

/// <summary>
/// Everything a phone is allowed to see, frozen at one moment. Phones are
/// spectators here - there is nothing to send back - so this is the whole
/// contract, and building it on the UI thread is what keeps the live session
/// off the server's threads entirely.
/// </summary>
public sealed record Snapshot(
    long Rev,
    string Mode,
    string Heading,
    ScoreLine Score,
    LeaderLine[] Leaders,
    UpcomingLine[] Upcoming)
{
    public static readonly Snapshot Waiting = new(
        0, "-", "Waiting for the scorer",
        new ScoreLine([], [], 0, 0, 1, 1, null), [], []);

    public static Snapshot From(Session session, long rev)
    {
        var view = session.View();

        var score = new ScoreLine(
            view.HomeNames, view.AwayNames,
            view.HomeScore, view.AwayScore,
            view.HomeTarget, view.AwayTarget,
            view.Breaking?.ToString().ToLowerInvariant());

        var leaders = session.Standings()
            .Select(row => new LeaderLine(row.Name, row.Wins, row.Played, row.Possible))
            .ToArray();

        return new Snapshot(rev, session.Config.Key, BuildHeading(session), score, leaders, BuildUpcoming(session));
    }

    private static string BuildHeading(Session session)
    {
        if (!session.IsRoundRobin) return session.Config.Label;

        return session.CurrentSet() is { } current
            ? $"{session.Config.Label} · Set {current.Match.Set} of {session.Config.Schedule.Length} · Round {current.Match.Round}"
            : $"{session.Config.Label} · all sets played";
    }

    private static UpcomingLine[] BuildUpcoming(Session session)
    {
        if (!session.IsRoundRobin) return [];

        return
        [
            .. session.Config.Schedule.Select(match =>
            {
                var line = session.RoundRobin.Scores.First(item => item.Set == match.Set);
                var current = session.RoundRobin.CurrentSet == match.Set;
                var first = Breaks.FirstBreakSide(line.BreakSwapped);

                return new UpcomingLine(
                    match.Set,
                    match.Round,
                    Names.TeamName(match.Home, session.NameFor),
                    Names.TeamName(match.Away, session.NameFor),
                    match.Sitting is null ? null : session.NameFor(match.Sitting),
                    line.Committed ? "played" : current ? "current" : "waiting",
                    line.Committed || line.RacksPlayed > 0 ? $"{line.Home} - {line.Away}" : null,
                    first.ToString().ToLowerInvariant());
            })
        ];
    }
}
