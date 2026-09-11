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

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoolScoreTracker",
        "session.json");

    public static Session Load()
    {
        try
        {
            if (!File.Exists(Path)) return new Session();

            var file = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(Path), Format);
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
