namespace PoolScoreTracker.Domain;

/// <summary>Whichever players are in a mode's line-up, by roster id and slot.</summary>
public abstract class ModeState
{
    public required List<string?> Lineup { get; init; }
}

/// <summary>A round-robin worked through its schedule, one set at a time.</summary>
public sealed class RoundRobinState : ModeState
{
    public required List<SetScore> Scores { get; init; }
    public int Target { get; set; } = 3;

    /// <summary>The set being played, or null once every set is committed.</summary>
    public int? CurrentSet { get; set; } = 1;
}

/// <summary>
/// A one-off race. Each side has its own target, so a handicap is just two
/// different numbers rather than a special case.
/// </summary>
public sealed class MatchState : ModeState
{
    public int TargetHome { get; set; } = 3;
    public int TargetAway { get; set; } = 3;
    public int Home { get; set; }
    public int Away { get; set; }
    public Side? Winner { get; set; }

    /// <summary>Who breaks the first rack. Swapping this is permanent here.</summary>
    public Side BreakSide { get; set; } = Side.Home;

    /// <summary>From New match, or the first rack if the race was started from cold.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Races already won and cleared away, oldest first - a night of several sets.</summary>
    public List<PlayedMatch> History { get; init; } = [];

    /// <summary>The games of the race on the board, and the clock on the one being played.</summary>
    public GameLog Games { get; } = new();

    public int RacksPlayed => Home + Away;
}

/// <summary>
/// A one-off race that was won and then cleared for the next one. Keeps the
/// roster ids for the player stats and the names as the scoreboard showed
/// them, so a player renamed or removed later still reads right here.
/// </summary>
public sealed record PlayedMatch(
    string?[] HomeIds,
    string?[] AwayIds,
    string[] HomeNames,
    string[] AwayNames,
    int Home,
    int Away,
    Side Winner,
    DateTimeOffset? StartedAt,
    DateTimeOffset FinishedAt,
    GameTime[] Games)
{
    /// <summary>The race's playing time: its games added up.</summary>
    public TimeSpan? Duration => GameLog.Sum(Games);
}

/// <summary>Whoever is scoring: the PC, or a phone by its key and the name it asked with.</summary>
public sealed record ScoreSource(string Id, string Name)
{
    public static readonly ScoreSource Pc = new("pc", "the PC");
}

/// <summary>
/// A rack turned away because another landed moments before - almost always
/// two people scoring the same rack. Says who got there first, so whoever
/// was refused knows the score already has it.
/// </summary>
public sealed record ScoreRefusal(ScoreSource By, TimeSpan Wait)
{
    public string MessageFor(ScoreSource asking)
    {
        var who = asking.Id == By.Id ? "You" : char.ToUpperInvariant(By.Name[0]) + By.Name[1..];
        var seconds = Math.Max(1, (int)Math.Ceiling(Wait.TotalSeconds));
        return $"{who} just updated the score. Try again in {seconds}s if that was another rack.";
    }
}

/// <summary>What the scoreboard and banner need, whichever mode is running.</summary>
public sealed record ScoreView(
    string[] HomeNames,
    string[] AwayNames,
    int HomeScore,
    int AwayScore,
    int HomeTarget,
    int AwayTarget,
    Side? Breaking);

/// <summary>
/// The live session: who is playing, what has been scored, and the operations
/// the scoring screen performs. Rules live in the other Domain types; this
/// holds the state and keeps the name map in step with it.
/// </summary>
public sealed class Session
{
    private static readonly string[] DefaultRoster =
        ["John", "Rob", "Shane", "Jim", "Richard"];

    /// <summary>
    /// How long after a rack is added before another one can be. No rack is
    /// played in five seconds, so a second plus inside it is somebody else
    /// scoring the same one.
    /// </summary>
    public static readonly TimeSpan ScoreCooldown = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, ModeState> _modes = [];
    private Dictionary<string, string> _names = [];
    private int _nextRosterId;

    /// <summary>The last rack added, by whom and when. A minus clears it.</summary>
    private (ScoreSource By, DateTimeOffset At)? _lastRack;

    private static DateTimeOffset Now => DateTimeOffset.Now;

    public Session(IEnumerable<string>? rosterNames = null)
    {
        var names = rosterNames?.ToList() ?? [.. DefaultRoster];
        Roster = [.. names.Select((name, i) => new RosterPlayer($"r{i + 1}", name))];
        _nextRosterId = Roster.Count + 1;

        foreach (var config in Modes.All.Values) _modes[config.Key] = BlankMode(config);

        FillEmptyLineupSlots();
        RefreshNames();
    }

    public List<RosterPlayer> Roster { get; private set; }
    public string ModeKey { get; private set; } = Modes.Doubles5;
    public BreakRule BreakRule { get; private set; } = BreakRule.Alternate;
    public NameStyle NameStyle { get; private set; } = NameStyle.First;

    /// <summary>
    /// Minutes after which the match on the table counts as running long and
    /// the scoreboard says so. 0 turns the warning off.
    /// </summary>
    public int LongMatchMinutes { get; private set; } = DefaultLongMatchMinutes;

    public const int DefaultLongMatchMinutes = 25;

    public ModeConfig Config => Modes.Get(ModeKey);
    public ModeState Current => _modes[ModeKey];
    public bool IsRoundRobin => Config.Kind == ModeKind.RoundRobin;

    public RoundRobinState RoundRobin => (RoundRobinState)Current;
    public MatchState Match => (MatchState)Current;

    public event EventHandler? Changed;

    private ModeState BlankMode(ModeConfig config)
    {
        var lineup = new List<string?>(new string?[config.Slots]);

        return config.Kind == ModeKind.RoundRobin
            ? new RoundRobinState
            {
                Lineup = lineup,
                Scores = [.. config.Schedule.Select(entry => new SetScore(entry.Set))]
            }
            : new MatchState { Lineup = lineup };
    }

    // --------------------------------------------------------- persistence

    public SessionFile Export()
    {
        var file = new SessionFile
        {
            Roster = [.. Roster.Select(player => new PlayerFile { Id = player.Id, Name = player.Name })],
            NextRosterId = _nextRosterId,
            Mode = ModeKey,
            BreakRule = BreakRule == BreakRule.Winner ? "winner" : "alternate",
            NameStyle = NameStyle == NameStyle.Full ? "full" : "first",
            LongMatchMinutes = LongMatchMinutes
        };

        foreach (var (key, state) in _modes)
        {
            var entry = new ModeFile { Lineup = [.. state.Lineup] };

            if (state is RoundRobinState rr)
            {
                entry.Target = rr.Target;
                entry.CurrentSet = rr.CurrentSet;
                entry.Scores =
                [
                    .. rr.Scores.Select(score => new ScoreFile
                    {
                        Set = score.Set,
                        Home = score.Home,
                        Away = score.Away,
                        Committed = score.Committed,
                        Winner = score.Winner?.ToString().ToLowerInvariant(),
                        BreakSwapped = score.BreakSwapped,
                        StartedAt = score.StartedAt,
                        FinishedAt = score.FinishedAt,
                        Games = ToFile(score.Games.Played),
                        GameStartedAt = score.Games.CurrentStartedAt
                    })
                ];
            }
            else if (state is MatchState match)
            {
                entry.TargetHome = match.TargetHome;
                entry.TargetAway = match.TargetAway;
                entry.Home = match.Home;
                entry.Away = match.Away;
                entry.Winner = match.Winner?.ToString().ToLowerInvariant();
                entry.BreakSide = match.BreakSide == Side.Away ? "away" : "home";
                entry.StartedAt = match.StartedAt;
                entry.FinishedAt = match.FinishedAt;
                entry.Games = ToFile(match.Games.Played);
                entry.GameStartedAt = match.Games.CurrentStartedAt;
                entry.History =
                [
                    .. match.History.Select(played => new PlayedFile
                    {
                        HomeIds = [.. played.HomeIds],
                        AwayIds = [.. played.AwayIds],
                        HomeNames = [.. played.HomeNames],
                        AwayNames = [.. played.AwayNames],
                        Home = played.Home,
                        Away = played.Away,
                        Winner = played.Winner == Side.Away ? "away" : "home",
                        StartedAt = played.StartedAt,
                        FinishedAt = played.FinishedAt,
                        Games = ToFile(played.Games)
                    })
                ];
            }

            file.Modes[key] = entry;
        }

        return file;
    }

    private static List<GameFile> ToFile(IEnumerable<GameTime> games) =>
    [
        .. games.Select(game => new GameFile
        {
            Winner = game.Winner == Side.Away ? "away" : "home",
            StartedAt = game.StartedAt,
            FinishedAt = game.FinishedAt
        })
    ];

    private static IEnumerable<GameTime> FromFile(IEnumerable<GameFile>? games) =>
        (games ?? [])
            .Where(game => game.FinishedAt is not null)
            .Select(game => new GameTime(game.Winner == "away" ? Side.Away : Side.Home, game.StartedAt, game.FinishedAt!.Value));

    /// <summary>
    /// Rebuilds a session from a saved file. Anything missing or unrecognised
    /// falls back to a fresh default rather than refusing to load, so an older
    /// or hand-edited file still opens.
    /// </summary>
    public static Session Restore(SessionFile file)
    {
        var names = file.Roster.Count > 0
            ? file.Roster.Select(player => player.Name).ToList()
            : [.. DefaultRoster];

        var session = new Session(names);

        // Keep the saved ids, so line-ups still point at the right people.
        if (file.Roster.Count > 0)
        {
            session.Roster = [.. file.Roster.Select(player => new RosterPlayer(player.Id, player.Name))];
            session._nextRosterId = Math.Max(file.NextRosterId, file.Roster.Count + 1);
        }

        if (Modes.All.ContainsKey(file.Mode)) session.ModeKey = file.Mode;
        session.BreakRule = file.BreakRule == "winner" ? BreakRule.Winner : BreakRule.Alternate;
        session.NameStyle = file.NameStyle == "full" ? NameStyle.Full : NameStyle.First;

        // Missing from files saved before the warning existed: they get the default.
        session.LongMatchMinutes = Math.Clamp(file.LongMatchMinutes ?? DefaultLongMatchMinutes, 0, 180);

        foreach (var (key, entry) in file.Modes)
        {
            if (!session._modes.TryGetValue(key, out var state)) continue;

            for (var slot = 0; slot < state.Lineup.Count; slot++)
            {
                state.Lineup[slot] = entry.Lineup.ElementAtOrDefault(slot);
            }

            if (state is RoundRobinState rr)
            {
                rr.Target = Math.Clamp(entry.Target, 1, 9);
                rr.CurrentSet = entry.CurrentSet;

                foreach (var saved in entry.Scores ?? [])
                {
                    var score = rr.Scores.FirstOrDefault(item => item.Set == saved.Set);
                    if (score is null) continue;

                    score.Home = Math.Clamp(saved.Home, 0, rr.Target);
                    score.Away = Math.Clamp(saved.Away, 0, rr.Target);
                    score.Committed = saved.Committed;
                    score.Winner = saved.Winner == "away" ? Side.Away : saved.Winner == "home" ? Side.Home : null;
                    score.BreakSwapped = saved.BreakSwapped;
                    score.StartedAt = saved.StartedAt;
                    score.FinishedAt = saved.FinishedAt;
                    score.Games.Played.AddRange(FromFile(saved.Games));
                    score.Games.CurrentStartedAt = saved.GameStartedAt;
                }
            }
            else if (state is MatchState match)
            {
                match.TargetHome = Math.Clamp(entry.TargetHome, 1, 9);
                match.TargetAway = Math.Clamp(entry.TargetAway, 1, 9);
                match.Home = Math.Clamp(entry.Home, 0, match.TargetHome);
                match.Away = Math.Clamp(entry.Away, 0, match.TargetAway);
                match.Winner = entry.Winner == "away" ? Side.Away : entry.Winner == "home" ? Side.Home : null;
                match.BreakSide = entry.BreakSide == "away" ? Side.Away : Side.Home;
                match.StartedAt = entry.StartedAt;
                match.FinishedAt = entry.FinishedAt;
                match.Games.Played.AddRange(FromFile(entry.Games));
                match.Games.CurrentStartedAt = entry.GameStartedAt;

                match.History.Clear();
                foreach (var saved in entry.History ?? [])
                {
                    if (saved.FinishedAt is not { } finished) continue;

                    match.History.Add(new PlayedMatch(
                        [.. saved.HomeIds], [.. saved.AwayIds],
                        [.. saved.HomeNames], [.. saved.AwayNames],
                        Math.Max(0, saved.Home), Math.Max(0, saved.Away),
                        saved.Winner == "away" ? Side.Away : Side.Home,
                        saved.StartedAt, finished, [.. FromFile(saved.Games)]));
                }
            }
        }

        session.FillEmptyLineupSlots();
        session.RefreshNames();
        return session;
    }

    // ------------------------------------------------------------- queries

    /// <summary>The round-robin set being played, if this is a round-robin.</summary>
    public (ScheduleEntry Match, SetScore Score)? CurrentSet()
    {
        if (!IsRoundRobin) return null;
        if (RoundRobin.CurrentSet is not int set) return null;

        var entry = Config.Schedule.FirstOrDefault(item => item.Set == set);
        if (entry is null) return null;

        return (entry, RoundRobin.Scores.First(score => score.Set == set));
    }

    public string NameFor(string token)
    {
        var index = Modes.TokenIndex(token);
        return NameForSlot(index) ?? $"P{index + 1}";
    }

    private string? NameForSlot(int index)
    {
        var id = Current.Lineup.ElementAtOrDefault(index);
        return id is not null && _names.TryGetValue(id, out var name) ? name : null;
    }

    /// <summary>
    /// The round robin's table, or in singles and doubles the same figures
    /// worked out from the races played so far tonight.
    /// </summary>
    public IReadOnlyList<StandingRow> Standings() =>
        IsRoundRobin
            ? Domain.Standings.Calculate(Config, RoundRobin.Scores, NameFor)
            : Domain.Standings.FromHistory(Match.History, id => Roster.FirstOrDefault(player => player.Id == id)?.Name);

    /// <summary>
    /// The match on the table - its games so far plus the one being played -
    /// the average match, and in a round robin when the last set should
    /// finish. Match times are rolled up from game times, so the time between
    /// one match ending and the next being started is not counted as play.
    /// </summary>
    public Timing Pace()
    {
        var now = Now;
        if (!IsRoundRobin)
        {
            var usualRace = Mean(Match.History.Select(played => played.Duration));
            return new Timing(Match.Games.Total(now), usualRace, null, 0, Match.Games.Current(now));
        }

        var scores = RoundRobin.Scores;
        var average = Mean(scores.Where(score => score.Committed).Select(score => score.Duration(now)));
        var left = scores.Count(score => !score.Committed);
        var current = CurrentSet()?.Score;

        return new Timing(current?.Duration(now), average, Forecast(now, current, left), left, current?.Games.Current(now));
    }

    /// <summary>
    /// When the last set should finish: the games still to play at the average
    /// game so far, plus the usual changeover before each set yet to start.
    /// Games to a set come from the sets played; until one is, a race is taken
    /// to run halfway between a sweep and hill-hill. Every game and set played
    /// feeds the averages, so the forecast firms up as the night goes on.
    /// </summary>
    private DateTimeOffset? Forecast(DateTimeOffset now, SetScore? current, int left)
    {
        var scores = RoundRobin.Scores;
        if (left == 0) return null;
        if (Mean(scores.SelectMany(score => score.Games.Played).Select(game => game.Duration)) is not { } perGame) return null;

        var target = RoundRobin.Target;
        var played = scores.Where(score => score.Committed).ToList();
        var perSet = played.Count > 0 ? played.Average(score => score.RacksPlayed) : (target + (target * 2 - 1)) / 2.0;
        var changeover = Mean(Changeovers(scores)) ?? TimeSpan.Zero;

        var rest = TimeSpan.Zero;
        var waiting = left;

        if (current is { Committed: false })
        {
            waiting--;

            // At least the racks a side still needs; more if sets usually go longer.
            var needed = Math.Max(0, target - Math.Max(current.Home, current.Away));
            var gamesLeft = needed == 0 ? 0 : Math.Max(needed, perSet - current.RacksPlayed);
            var thisSet = perGame * gamesLeft - (current.Games.Current(now) ?? TimeSpan.Zero);
            if (thisSet > TimeSpan.Zero) rest += thisSet;
            if (current.StartedAt is null) rest += changeover;
        }

        rest += (perGame * perSet + changeover) * waiting;
        return now + rest;
    }

    /// <summary>
    /// The gaps between one set ending and the next being started. Anything
    /// over half an hour is a break rather than a changeover, and left out so
    /// one meal does not push every set's forecast back.
    /// </summary>
    private static IEnumerable<TimeSpan?> Changeovers(IEnumerable<SetScore> scores)
    {
        var started = scores.Where(score => score.StartedAt is not null).OrderBy(score => score.StartedAt).ToList();

        for (var i = 1; i < started.Count; i++)
        {
            if (started[i - 1].FinishedAt is not { } finished) continue;

            var gap = started[i].StartedAt!.Value - finished;
            if (gap >= TimeSpan.Zero && gap <= TimeSpan.FromMinutes(30)) yield return gap;
        }
    }

    private static TimeSpan? Mean(IEnumerable<TimeSpan?> lengths)
    {
        var known = lengths.OfType<TimeSpan>().ToList();
        return known.Count > 0 ? TimeSpan.FromTicks((long)known.Average(length => length.Ticks)) : null;
    }

    /// <summary>
    /// What the scoreboard's centre button says: Start before a match has
    /// begun, then nothing while it is played, then Next match (New match in
    /// singles and doubles) once a side has won. Null when there is nothing
    /// to press.
    /// </summary>
    public string? CentreAction()
    {
        if (IsRoundRobin)
        {
            if (CurrentSet() is not { } current || current.Score.Committed) return null;

            var score = current.Score;
            var target = RoundRobin.Target;
            if ((score.Home >= target) ^ (score.Away >= target)) return "Next match";
            return score.StartedAt is null && score.RacksPlayed == 0 ? "Start" : null;
        }

        if (Match.Winner is not null) return "New match";
        return Match.StartedAt is null && Match.RacksPlayed == 0 ? "Start" : null;
    }

    /// <summary>
    /// Presses the centre button, whatever it says just now. A press that
    /// arrives after the button has changed does nothing it was not meant to:
    /// Start only starts, and moving on only happens once a side has won.
    /// </summary>
    public void PressCentre()
    {
        switch (CentreAction())
        {
            case "Start":
                StartMatch();
                break;
            case "Next match":
                TryCommit(out _);
                break;
            case "New match":
                StartNewMatch();
                break;
        }
    }

    /// <summary>The players are at the table: the match and its first game are on the clock.</summary>
    public void StartMatch()
    {
        if (CentreAction() != "Start") return;

        var now = Now;
        if (IsRoundRobin && CurrentSet() is { } current)
        {
            current.Score.StartedAt = now;
            current.Score.Games.CurrentStartedAt = now;
        }
        else if (!IsRoundRobin)
        {
            Match.StartedAt = now;
            Match.Games.CurrentStartedAt = now;
        }

        Notify();
    }

    /// <summary>The games log being added to now: the set on the table, or the race on the board.</summary>
    private GameLog? LiveGames() =>
        IsRoundRobin
            ? CurrentSet() is { } current && !current.Score.Committed ? current.Score.Games : null
            : Match.Games;

    /// <summary>
    /// Starts the clock on the game being played again from now - for when a
    /// run of taps has left it timing from the wrong moment.
    /// </summary>
    public void RestartGameClock()
    {
        if (LiveGames() is not { } games) return;

        games.CurrentStartedAt = Now;
        Notify();
    }

    /// <summary>
    /// Fastest, slowest and average game: across the round robin's sets, or
    /// tonight's singles or doubles races including the one on the board.
    /// </summary>
    public GameSummary Games() =>
        GameSummary.Of(IsRoundRobin
            ? RoundRobin.Scores.SelectMany(score => score.Games.Played)
            : Match.History.SelectMany(played => played.Games).Concat(Match.Games.Played));

    /// <summary>Who breaks the rack about to be played, if the app can know.</summary>
    public Side? BreakingNext()
    {
        if (IsRoundRobin)
        {
            if (CurrentSet() is not { } current) return null;

            var (_, score) = current;
            var opener = Breaks.FirstBreakSide(score.BreakSwapped);
            var over = Math.Max(score.Home, score.Away) >= RoundRobin.Target;
            return Breaks.RackBreakSide(opener, score.RacksPlayed, over, BreakRule);
        }

        return Breaks.RackBreakSide(Match.BreakSide, Match.RacksPlayed, Match.Winner is not null, BreakRule);
    }

    /// <summary>Everything the scoreboard and banner draw, for either mode kind.</summary>
    public ScoreView View()
    {
        if (IsRoundRobin)
        {
            if (CurrentSet() is not { } current) return new ScoreView([], [], 0, 0, 1, 1, null);

            var (entry, score) = current;
            return new ScoreView(
                [.. entry.Home.Select(NameFor)],
                [.. entry.Away.Select(NameFor)],
                score.Home, score.Away,
                RoundRobin.Target, RoundRobin.Target,
                BreakingNext());
        }

        var size = Config.TeamSize;
        return new ScoreView(
            [.. Enumerable.Range(0, size).Select(i => NameForSlot(i) ?? SideLabel(Side.Home, i, size))],
            [.. Enumerable.Range(0, size).Select(i => NameForSlot(size + i) ?? SideLabel(Side.Away, i, size))],
            Match.Home, Match.Away,
            Match.TargetHome, Match.TargetAway,
            BreakingNext());
    }

    private static string SideLabel(Side side, int index, int teamSize)
    {
        var letter = side == Side.Home ? "A" : "B";
        return teamSize == 1 ? $"Player {letter}" : $"{letter}{index + 1}";
    }

    /// <summary>What a line-up slot is called on the setup screen.</summary>
    public string SlotLabel(int index)
    {
        if (IsRoundRobin) return $"P{index + 1}";
        if (Config.TeamSize == 1) return index == 0 ? "Player A" : "Player B";
        return $"Team {(index < Config.TeamSize ? "A" : "B")} ({index % Config.TeamSize + 1})";
    }

    // ------------------------------------------------------- scoring moves

    /// <summary>
    /// A rack on or off one side. A rack added within <see cref="ScoreCooldown"/>
    /// of the last one is turned away, and the refusal comes back to be shown
    /// to whoever tried; taking one off is never held up, and lets the next
    /// plus straight through, so putting right a tap on the wrong side is not.
    /// </summary>
    public ScoreRefusal? Adjust(Side side, int delta, ScoreSource by)
    {
        var now = Now;

        if (IsRoundRobin)
        {
            if (CurrentSet() is not { } current) return null;

            var (_, score) = current;
            if (score.Committed) return null;

            var before = side == Side.Home ? score.Home : score.Away;
            var after = Math.Clamp(before + delta, 0, RoundRobin.Target);
            if (after == before) return null;
            if (Hold(after > before, by, now) is { } refusal) return refusal;

            if (side == Side.Home) score.Home = after;
            else score.Away = after;

            score.StartedAt ??= now;
            Log(score.Games, side, after > before, now);

            // Won: no game is being played until the next set starts.
            if (Math.Max(score.Home, score.Away) >= RoundRobin.Target) score.Games.CurrentStartedAt = null;
        }
        else
        {
            var before = side == Side.Home ? Match.Home : Match.Away;
            var after = Math.Clamp(before + delta, 0, side == Side.Home ? Match.TargetHome : Match.TargetAway);
            if (after == before) return null;
            if (Hold(after > before, by, now) is { } refusal) return refusal;

            if (side == Side.Home) Match.Home = after;
            else Match.Away = after;

            Match.StartedAt ??= now;
            Log(Match.Games, side, after > before, now);
            RefreshMatchWinner();
            if (Match.Winner is not null) Match.Games.CurrentStartedAt = null;
        }

        Notify();
        return null;
    }

    private static void Log(GameLog games, Side side, bool won, DateTimeOffset now)
    {
        if (won) games.Won(side, now);
        else games.TakeBack(side);
    }

    /// <summary>Turns away a rack that follows the last one too closely, else records it.</summary>
    private ScoreRefusal? Hold(bool adding, ScoreSource by, DateTimeOffset now)
    {
        if (!adding)
        {
            _lastRack = null;
            return null;
        }

        if (_lastRack is { } last && now - last.At < ScoreCooldown)
        {
            return new ScoreRefusal(last.By, ScoreCooldown - (now - last.At));
        }

        _lastRack = (by, now);
        return null;
    }

    /// <summary>
    /// Commits the current set. A side has to have reached the race and be
    /// ahead; otherwise nothing happens and the reason comes back.
    /// </summary>
    public bool TryCommit(out string? problem)
    {
        problem = null;

        if (!IsRoundRobin)
        {
            problem = "A one-off match has no sets to commit - use New match to start again.";
            return false;
        }

        if (CurrentSet() is not { } current) return false;
        var (_, score) = current;

        var target = RoundRobin.Target;
        if (Math.Max(score.Home, score.Away) < target || score.Home == score.Away)
        {
            problem = $"A side has to reach {target} and lead before the set can be committed.";
            return false;
        }

        var now = Now;
        score.Committed = true;
        score.Winner = score.Home > score.Away ? Side.Home : Side.Away;
        score.StartedAt ??= now;
        score.FinishedAt = now;
        _lastRack = null;

        // Next uncommitted set after this one, else the first one still open.
        var next = RoundRobin.Scores.FirstOrDefault(item => !item.Committed && item.Set > score.Set)
                   ?? RoundRobin.Scores.FirstOrDefault(item => !item.Committed);
        RoundRobin.CurrentSet = next?.Set;

        // The next set waits for its Start, so the changeover is not counted
        // as play.

        Notify();
        return true;
    }

    public void UndoLastCommit()
    {
        if (!IsRoundRobin) return;

        var last = RoundRobin.Scores.Where(score => score.Committed).MaxBy(score => score.Set);
        if (last is null) return;

        // The set it moved on to never got going, so its clock goes too.
        if (CurrentSet() is { } current && current.Score.RacksPlayed == 0)
        {
            current.Score.StartedAt = null;
            current.Score.Games.Clear();
        }

        last.Committed = false;
        last.Winner = null;
        last.FinishedAt = null;
        RoundRobin.CurrentSet = last.Set;
        _lastRack = null;

        Notify();
    }

    /// <summary>
    /// Wipes this format's scores and starts it again from the first set. The
    /// players, the line-up and the other formats are left alone.
    /// </summary>
    public void ResetScores()
    {
        if (IsRoundRobin)
        {
            foreach (var score in RoundRobin.Scores)
            {
                score.Home = 0;
                score.Away = 0;
                score.Committed = false;
                score.Winner = null;
                score.BreakSwapped = false;
                score.StartedAt = null;
                score.FinishedAt = null;
                score.Games.Clear();
            }

            RoundRobin.CurrentSet = Config.Schedule.FirstOrDefault()?.Set;
        }
        else
        {
            RecordMatch();
            ClearMatch();
        }

        _lastRack = null;
        Notify();
    }

    /// <summary>Clears a one-off match back to nil-nil.</summary>
    public void StartNewMatch()
    {
        if (IsRoundRobin) return;

        RecordMatch();
        ClearMatch();

        // The next race waits for its Start.
        _lastRack = null;

        Notify();
    }

    /// <summary>
    /// Files a won race in the history before the board is cleared - by New
    /// match, Clear scores or a change of player. A race nobody has won yet
    /// is simply dropped, as it always was.
    /// </summary>
    private void RecordMatch()
    {
        if (Match.Winner is not { } winner) return;

        var view = View();
        var size = Config.TeamSize;
        var lineup = Match.Lineup;

        Match.History.Add(new PlayedMatch(
            [.. lineup.Take(size)], [.. lineup.Skip(size).Take(size)],
            view.HomeNames, view.AwayNames,
            Match.Home, Match.Away, winner,
            Match.StartedAt, Match.FinishedAt ?? Now,
            [.. Match.Games.Played]));
    }

    /// <summary>Forgets tonight's singles or doubles races. The one on the board is left alone.</summary>
    public void ClearHistory()
    {
        if (IsRoundRobin || Match.History.Count == 0) return;

        Match.History.Clear();
        Notify();
    }

    private void ClearMatch()
    {
        Match.Home = 0;
        Match.Away = 0;
        Match.Winner = null;
        Match.StartedAt = null;
        Match.FinishedAt = null;
        Match.Games.Clear();
    }

    /// <summary>
    /// Flips who breaks. In a round-robin that is the current set only; in a
    /// one-off match it is permanent, so the order players were picked in does
    /// not decide the break.
    /// </summary>
    public void SwapBreak()
    {
        if (IsRoundRobin)
        {
            if (CurrentSet() is not { } current) return;
            current.Score.BreakSwapped = !current.Score.BreakSwapped;
        }
        else
        {
            Match.BreakSide = Match.BreakSide.Opposite();
        }

        Notify();
    }

    // -------------------------------------------------------------- setup

    public void SetMode(string modeKey)
    {
        if (!_modes.ContainsKey(modeKey) || modeKey == ModeKey) return;

        ModeKey = modeKey;
        _lastRack = null;
        RefreshNames();
        Notify();
    }

    public void SetBreakRule(BreakRule rule)
    {
        if (rule == BreakRule) return;
        BreakRule = rule;
        Notify();
    }

    public void SetNameStyle(NameStyle style)
    {
        if (style == NameStyle) return;
        NameStyle = style;
        RefreshNames();
        Notify();
    }

    public void SetLongMatchMinutes(int minutes)
    {
        var next = Math.Clamp(minutes, 0, 180);
        if (next == LongMatchMinutes) return;
        LongMatchMinutes = next;
        Notify();
    }

    /// <summary>The match on the table has gone past the long-match warning.</summary>
    public bool IsRunningLong(Timing pace) =>
        LongMatchMinutes > 0 && pace.Current is { } length && length >= TimeSpan.FromMinutes(LongMatchMinutes);

    /// <summary>
    /// Sets the race. In a round-robin one number covers both sides and any
    /// uncommitted score is pulled back inside it; in a match each side has
    /// its own, which is how handicaps work.
    /// </summary>
    public void SetTarget(Side side, int target)
    {
        var next = Math.Clamp(target, MinTarget, MaxTarget);

        if (IsRoundRobin)
        {
            if (next == RoundRobin.Target) return;
            RoundRobin.Target = next;

            foreach (var score in RoundRobin.Scores.Where(score => !score.Committed))
            {
                score.Home = Math.Clamp(score.Home, 0, next);
                score.Away = Math.Clamp(score.Away, 0, next);
                score.Games.Trim(Side.Home, score.Home);
                score.Games.Trim(Side.Away, score.Away);
            }
        }
        else
        {
            if (side == Side.Home)
            {
                if (next == Match.TargetHome) return;
                Match.TargetHome = next;
                Match.Home = Math.Clamp(Match.Home, 0, next);
                Match.Games.Trim(Side.Home, Match.Home);
            }
            else
            {
                if (next == Match.TargetAway) return;
                Match.TargetAway = next;
                Match.Away = Math.Clamp(Match.Away, 0, next);
                Match.Games.Trim(Side.Away, Match.Away);
            }

            RefreshMatchWinner();
        }

        Notify();
    }

    public const int MinTarget = 1;
    public const int MaxTarget = 9;

    public int TargetFor(Side side) =>
        IsRoundRobin ? RoundRobin.Target : side == Side.Home ? Match.TargetHome : Match.TargetAway;

    // -------------------------------------------------------------- roster

    public void AddPlayer(string name) => AddPlayers([name]);

    /// <summary>
    /// Adds everyone in one go. Blanks are skipped, and the whole lot counts as
    /// a single change - adding a team one name at a time would otherwise save
    /// and re-publish once per player.
    /// </summary>
    public void AddPlayers(IEnumerable<string> names)
    {
        var added = false;

        foreach (var name in names)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0) continue;

            Roster.Add(new RosterPlayer($"r{_nextRosterId++}", trimmed));
            added = true;
        }

        if (!added) return;

        FillEmptyLineupSlots();
        RefreshNames();
        Notify();
    }

    public void RenamePlayer(string id, string name)
    {
        var player = Roster.FirstOrDefault(entry => entry.Id == id);
        if (player is null) return;

        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed == player.Name) return;

        player.Name = trimmed;
        RefreshNames();
        Notify();
    }

    public void RemovePlayer(string id)
    {
        if (Roster.All(player => player.Id != id)) return;

        Roster = [.. Roster.Where(player => player.Id != id)];

        // Never leave the app with nobody on the list.
        if (Roster.Count == 0)
        {
            Roster = [.. DefaultRoster.Select((name, i) => new RosterPlayer($"r{i + 1}", name))];
            _nextRosterId = Roster.Count + 1;
        }

        foreach (var mode in _modes.Values)
        {
            for (var i = 0; i < mode.Lineup.Count; i++)
            {
                if (mode.Lineup[i] == id) mode.Lineup[i] = null;
            }
        }

        FillEmptyLineupSlots();
        RefreshNames();
        Notify();
    }

    /// <summary>
    /// Puts a player in a slot. If they were already in another slot the two
    /// swap, so a line-up can never hold the same player twice.
    /// </summary>
    public void SetLineupSlot(int slot, string? rosterId)
    {
        var lineup = Current.Lineup;
        if (slot < 0 || slot >= lineup.Count) return;
        if (lineup[slot] == rosterId) return;

        // A race already won goes in the history under the players who won it.
        if (!IsRoundRobin) RecordMatch();

        if (rosterId is not null)
        {
            var existing = lineup.IndexOf(rosterId);
            if (existing != -1 && existing != slot) lineup[existing] = lineup[slot];
        }

        lineup[slot] = rosterId;

        // Changing who is playing invalidates a one-off match in progress.
        if (!IsRoundRobin)
        {
            ClearMatch();
            _lastRack = null;
        }

        RefreshNames();
        Notify();
    }

    /// <summary>Drops unused roster players into any empty slots, across every mode.</summary>
    private void FillEmptyLineupSlots()
    {
        foreach (var mode in _modes.Values)
        {
            var used = mode.Lineup.Where(id => id is not null).ToHashSet();
            var spare = new Queue<string>(Roster.Select(player => player.Id).Where(id => !used.Contains(id)));

            for (var i = 0; i < mode.Lineup.Count && spare.Count > 0; i++)
            {
                if (mode.Lineup[i] is not null) continue;
                mode.Lineup[i] = spare.Dequeue();
            }
        }
    }

    /// <summary>Who has won the one-off race, if anyone, with the clock stopped or restarted to match.</summary>
    private void RefreshMatchWinner()
    {
        Match.Winner = Match.Home >= Match.TargetHome ? Side.Home
            : Match.Away >= Match.TargetAway ? Side.Away
            : null;

        if (Match.Winner is null) Match.FinishedAt = null;
        else Match.FinishedAt ??= Now;
    }

    private void RefreshNames() =>
        _names = Names.BuildNameMap(
            Current.Lineup,
            Names.UseFullNames(Config, NameStyle),
            id => Roster.FirstOrDefault(player => player.Id == id)?.Name);

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
