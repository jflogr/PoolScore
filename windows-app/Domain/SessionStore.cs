using System.Text.Json;

namespace PoolScoreTracker.Domain;

public sealed class PlayerFile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ScoreFile
{
    public int Set { get; set; }
    public int Home { get; set; }
    public int Away { get; set; }
    public bool Committed { get; set; }
    public string? Winner { get; set; }
    public bool BreakSwapped { get; set; }
}

/// <summary>
/// One mode on disk. Both kinds share a shape so the file stays flat and
/// readable; a round-robin fills in Scores, a match fills in the rest.
/// </summary>
public sealed class ModeFile
{
    public List<string?> Lineup { get; set; } = [];

    public int Target { get; set; } = 3;
    public int? CurrentSet { get; set; }
    public List<ScoreFile>? Scores { get; set; }

    public int TargetHome { get; set; } = 3;
    public int TargetAway { get; set; } = 3;
    public int Home { get; set; }
    public int Away { get; set; }
    public string? Winner { get; set; }
    public string BreakSide { get; set; } = "home";
}

public sealed class SessionFile
{
    /// <summary>3 is the native app. 1 and 2 were the browser version's localStorage.</summary>
    public int Version { get; set; } = 3;

    public List<PlayerFile> Roster { get; set; } = [];
    public int NextRosterId { get; set; }
    // Qualified because the Modes property below shadows the Modes class here.
    public string Mode { get; set; } = PoolScoreTracker.Domain.Modes.Doubles5;
    public string BreakRule { get; set; } = "alternate";
    public string NameStyle { get; set; } = "first";
    public Dictionary<string, ModeFile> Modes { get; set; } = [];
}

/// <summary>
/// Keeps the session on disk between runs. Nothing here is allowed to take the
/// app down: a scoreboard that will not open because a save file went bad is
/// worse than one that forgot last night's game.
/// </summary>
public static class SessionStore
{
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// One session per Windows account, wherever the exe happens to be. Kept
    /// out of the app's own folder so the exe stays a single file you can drop
    /// anywhere without dragging a second one along behind it.
    /// </summary>
    public static string Path { get; } =
        System.IO.Path.Combine(LocalAppData, "PoolScore", "session.json");

    /// <summary>
    /// Where the builds before the PoolScore rename kept theirs. Read from as a
    /// starting point and never written to, so an older exe still opens on its
    /// own players and scores if this one has to be abandoned.
    /// </summary>
    public static string Previous { get; } =
        System.IO.Path.Combine(LocalAppData, "PoolScoreTracker", "session.json");

    public static Session Load()
    {
        try
        {
            // Our own file if we have one; otherwise take the last version's as
            // a starting point. Saves only ever go to ours, so the old app is
            // left exactly as it was and stays usable if this one goes wrong.
            var source = File.Exists(Path) ? Path
                : File.Exists(Previous) ? Previous
                : null;

            if (source is null) return new Session();

            var file = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(source), Format);
            return file is null ? new Session() : Session.Restore(file);
        }
        catch (Exception)
        {
            // A corrupt or half-written file should cost you the session, not the app.
            return new Session();
        }
    }

    public static void Save(Session session)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(directory);

            // Written beside the real file and moved into place, so a crash
            // mid-write cannot leave a truncated session behind.
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(session.Export(), Format));
            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception)
        {
            // Out of disk, or the folder is locked. Nothing to do about it here.
        }
    }
}
