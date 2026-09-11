namespace PoolScoreTracker.Domain;

public enum ModeKind
{
    /// <summary>A round-robin worked through a fixed schedule of sets.</summary>
    RoundRobin,

    /// <summary>A one-off race, with a handicap allowed on each side.</summary>
    Match
}

public enum Side
{
    Home,
    Away
}

public enum BreakRule
{
    /// <summary>The break changes hands after every rack, so the app can follow it.</summary>
    Alternate,

    /// <summary>Whoever won the last rack breaks. Only totals are kept, so the app cannot know.</summary>
    Winner
}

public enum NameStyle
{
    First,
    Full
}

/// <summary>
/// One set in a schedule. Sides are position tokens (p1..pN), resolved to real
/// players through the active mode's line-up, so a schedule never has to know
/// who is actually playing.
/// </summary>
public sealed record ScheduleEntry(int Set, int Round, string[] Home, string[] Away, string? Sitting);

public sealed record ModeConfig(
    string Key,
    string Label,
    ModeKind Kind,
    int TeamSize,
    int Slots,
    ScheduleEntry[] Schedule);

public static class Modes
{
    public const string Doubles5 = "doubles5";
    public const string Doubles4 = "doubles4";
    public const string Singles = "singles";
    public const string Match2 = "match2";

    /// <summary>
    /// 5-player doubles: 15 sets, everyone plays 12 and breaks first in 6.
    /// Dead even. The home side breaks first unless that set has been swapped.
    /// </summary>
    private static readonly ScheduleEntry[] Doubles5Schedule =
    [
        new(1, 1, ["p1", "p2"], ["p3", "p4"], "p5"),
        new(2, 1, ["p2", "p3"], ["p4", "p5"], "p1"),
        new(3, 1, ["p1", "p4"], ["p3", "p5"], "p2"),
        new(4, 1, ["p1", "p5"], ["p2", "p4"], "p3"),
        new(5, 1, ["p1", "p3"], ["p2", "p5"], "p4"),
        new(6, 2, ["p1", "p4"], ["p2", "p3"], "p5"),
        new(7, 2, ["p2", "p4"], ["p3", "p5"], "p1"),
        new(8, 2, ["p3", "p4"], ["p1", "p5"], "p2"),
        new(9, 2, ["p4", "p5"], ["p1", "p2"], "p3"),
        new(10, 2, ["p2", "p3"], ["p1", "p5"], "p4"),
        new(11, 3, ["p1", "p3"], ["p2", "p4"], "p5"),
        new(12, 3, ["p2", "p5"], ["p3", "p4"], "p1"),
        new(13, 3, ["p4", "p5"], ["p1", "p3"], "p2"),
        new(14, 3, ["p2", "p5"], ["p1", "p4"], "p3"),
        new(15, 3, ["p3", "p5"], ["p1", "p2"], "p4")
    ];

    private static readonly ScheduleEntry[] Doubles4Schedule = BuildDoubles4Schedule();

    /// <summary>
    /// 4-player doubles: everyone partners everyone once per round and nobody
    /// sits. Each round plays the same three pairings; which side breaks rotates
    /// so the break is shared. 9 sets hand out 18 first breaks between 4 players,
    /// so a dead even 4.5 each is impossible - the closest any 9-set schedule
    /// gets is 6/4/4/4, with the extra going to P1.
    /// </summary>
    private static ScheduleEntry[] BuildDoubles4Schedule()
    {
        // Per round, which of the three teams containing P1 breaks: p1p2, p1p3, p1p4.
        bool[][] p1Breaks =
        [
            [true, true, true],    // round 1: P1 breaks all three
            [false, false, true],  // round 2: P4's side breaks twice
            [true, true, false]    // round 3: back to P1's side twice
        ];

        (string[] With1, string[] Other)[] pairings =
        [
            (["p1", "p2"], ["p3", "p4"]),
            (["p1", "p3"], ["p2", "p4"]),
            (["p1", "p4"], ["p2", "p3"])
        ];

        var output = new List<ScheduleEntry>();
        var set = 1;

        for (var round = 0; round < p1Breaks.Length; round++)
        {
            for (var pair = 0; pair < pairings.Length; pair++)
            {
                var p1Home = p1Breaks[round][pair];
                var (with1, other) = pairings[pair];

                output.Add(new ScheduleEntry(
                    set,
                    round + 1,
                    p1Home ? with1 : other,
                    p1Home ? other : with1,
                    null));

                set++;
            }
        }

        return [.. output];
    }

    public static readonly IReadOnlyDictionary<string, ModeConfig> All =
        new Dictionary<string, ModeConfig>
        {
            // Named for what they are rather than in shorthand - "2P RR 5" told
            // you nothing unless you already knew, and hid the fact that the
            // doubles match is just "pick two teams and play one race".
            [Doubles5] = new(Doubles5, "Round robin (5 players)", ModeKind.RoundRobin, 2, 5, Doubles5Schedule),
            [Doubles4] = new(Doubles4, "Round robin (4 players)", ModeKind.RoundRobin, 2, 4, Doubles4Schedule),
            [Singles] = new(Singles, "Singles (1 v 1)", ModeKind.Match, 1, 2, []),
            [Match2] = new(Match2, "Doubles (2 v 2)", ModeKind.Match, 2, 4, [])
        };

    public static ModeConfig Get(string key) => All[key];

    /// <summary>Position token (p1..pN) to its zero-based line-up index.</summary>
    public static int TokenIndex(string token) => int.Parse(token[1..]) - 1;

    public static Side Opposite(this Side side) => side == Side.Home ? Side.Away : Side.Home;
}
