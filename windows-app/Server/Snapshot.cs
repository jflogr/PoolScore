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

public sealed record SlotLine(string Label, string? Player);

public sealed record PlayerLine(string Id, string Name);

/// <summary>
/// What an allowed phone can change in a one-off match: each side's race, who
/// is in each slot, and who is on the roster to pick from. Round robins have
/// none - their setup is a whole evening's schedule and stays at the PC.
/// </summary>
public sealed record SetupLine(
    int HomeTarget,
    int AwayTarget,
    int MinTarget,
    int MaxTarget,
    SlotLine[] Slots,
    PlayerLine[] Players);

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
/// Everything a phone is allowed to see, frozen at one moment. Building it on
/// the UI thread is what keeps the live session off the server's threads
/// entirely.
///
/// <see cref="Game"/> names the game on screen. A phone sends it back with a
/// tap, and a tap for a game that is no longer on screen is dropped - so a
/// phone that has not caught up yet cannot score into the next set.
/// </summary>
public sealed record Snapshot(
    long Rev,
    string Mode,
    string Heading,
    string Game,
    bool CanScore,
    ScoreLine Score,
    LeaderLine[] Leaders,
    UpcomingLine[] Upcoming,
    SetupLine? Setup)
{
    public static readonly Snapshot Waiting = new(
        0, "-", "Waiting for the scorer", "-", false,
        new ScoreLine([], [], 0, 0, 1, 1, null), [], [], null);

    /// <summary>
    /// Which game is being scored: the set in a round robin, the mode in a
    /// one-off match. Nothing once every set is played.
    /// </summary>
    public static string GameFor(Session session)
    {
        if (!session.IsRoundRobin) return session.Config.Key;

        return session.CurrentSet() is { } current && !current.Score.Committed
            ? $"{session.Config.Key}/set-{current.Match.Set}"
            : "-";
    }

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

        var game = GameFor(session);

        return new Snapshot(
            rev, session.Config.Key, BuildHeading(session),
            game, game != "-",
            score, leaders, BuildUpcoming(session), BuildSetup(session));
    }

    private static SetupLine? BuildSetup(Session session)
    {
        if (session.IsRoundRobin) return null;

        return new SetupLine(
            session.Match.TargetHome, session.Match.TargetAway,
            Session.MinTarget, Session.MaxTarget,
            [.. session.Current.Lineup.Select((id, slot) => new SlotLine(session.SlotLabel(slot), id))],
            [.. session.Roster.Select(player => new PlayerLine(player.Id, player.Name))]);
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
