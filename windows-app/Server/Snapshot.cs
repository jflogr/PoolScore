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

/// <summary>Possible is null in singles and doubles, where there is no schedule left to play.</summary>
public sealed record LeaderLine(string Name, int Wins, int Played, int? Possible, int RacksWon, int RacksLost);

/// <summary>A singles or doubles race already played tonight.</summary>
public sealed record HistoryLine(int Number, string Home, string Away, string Score, string Winner, string Finished, string? Time);

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
    string? BreaksFirst,
    string? Time);

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
    SetupLine? Setup,
    string? Pace,
    HistoryLine[] History)
{
    public static readonly Snapshot Waiting = new(
        0, "-", "Waiting for the scorer", "-", false,
        new ScoreLine([], [], 0, 0, 1, 1, null), [], [], null, null, []);

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
            .Select(row => new LeaderLine(
                row.Name, row.Wins, row.Played, session.IsRoundRobin ? row.Possible : null, row.RacksWon, row.RacksLost))
            .ToArray();

        var game = GameFor(session);

        return new Snapshot(
            rev, session.Config.Key, BuildHeading(session),
            game, game != "-",
            score, leaders, BuildUpcoming(session), BuildSetup(session), BuildPace(session),
            BuildHistory(session));
    }

    /// <summary>Newest first, the way the PC lists them.</summary>
    private static HistoryLine[] BuildHistory(Session session)
    {
        if (session.IsRoundRobin) return [];

        return
        [
            .. session.Match.History
                .Select((played, i) => new HistoryLine(
                    i + 1,
                    string.Join(" / ", played.HomeNames),
                    string.Join(" / ", played.AwayNames),
                    $"{played.Home} - {played.Away}",
                    played.Winner == Side.Away ? "away" : "home",
                    Timing.TimeOfDay(played.FinishedAt),
                    played.Duration is { } length ? Timing.Clock(length) : null))
                .Reverse()
        ];
    }

    /// <summary>
    /// The average set and the projected finish, as of this snapshot. Phones
    /// get a fresh one with every rack, which is often enough for a forecast.
    /// </summary>
    private static string? BuildPace(Session session)
    {
        var pace = session.Pace();
        if (pace.Average is not { } average) return null;

        if (!session.IsRoundRobin)
        {
            var played = session.Match.History.Count;
            return $"{played} played · average match {Timing.Clock(average)}";
        }

        var line = $"Average match {Timing.Clock(average)}";
        return pace.Finish is { } finish ? $"{line} · finish about {Timing.TimeOfDay(finish)}" : line;
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

        var now = DateTimeOffset.Now;

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
                    first.ToString().ToLowerInvariant(),
                    line.Committed && line.Duration(now) is { } length ? Timing.Clock(length) : null);
            })
        ];
    }
}
