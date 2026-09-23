using System.Text.RegularExpressions;

namespace PoolScoreTracker.Server;

public enum PhoneStatus
{
    /// <summary>Asked to score; waiting on the PC.</summary>
    Pending,

    /// <summary>Allowed at the PC. Its taps are applied.</summary>
    Approved
}

/// <summary>A phone as the PC shows it. The phone's key never leaves this class.</summary>
public sealed record ScoringPhone(int Id, string Name, PhoneStatus Status);

/// <summary>A phone's standing changed, so it should be told. Carries its key.</summary>
public sealed record AccessChange(string Key, string Status);

/// <summary>
/// Which phones may score. A phone makes up its own random key and keeps it;
/// asking to score files that key as pending, and allowing it at the PC is
/// what makes taps carrying it count. Anyone can scan the code and watch, but
/// nothing they send changes the score until somebody at the PC says yes.
///
/// Held in memory only, so every approval ends when the app closes and each
/// match night starts with nobody allowed.
///
/// Called from request threads and the UI thread alike, so everything goes
/// through one lock, and events are raised after it is released.
/// </summary>
public sealed partial class PhoneAccess
{
    /// <summary>
    /// Enough for a table full of friends; a cap so a phone left hammering
    /// the button cannot bury the PC in requests.
    /// </summary>
    private const int MaxPending = 6;

    private const int MaxApproved = 12;
    private const int MaxName = 24;

    private sealed class Entry(string key, int id, string name)
    {
        public string Key { get; } = key;
        public int Id { get; } = id;
        public string Name { get; set; } = name;
        public PhoneStatus Status { get; set; } = PhoneStatus.Pending;
    }

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _byKey = new(StringComparer.Ordinal);
    private int _nextId;

    /// <summary>Anything on the PC's list changed. Arrives on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>One phone should hear its new standing. Arrives on any thread.</summary>
    public event EventHandler<AccessChange>? StatusChanged;

    /// <summary>32 hex characters, as the phone page makes them.</summary>
    public static bool IsKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex KeyPattern();

    /// <summary>none, pending or approved.</summary>
    public string StatusOf(string? key)
    {
        if (!IsKey(key)) return "none";

        lock (_gate)
        {
            return _byKey.TryGetValue(key!, out var entry) ? Word(entry.Status) : "none";
        }
    }

    public bool IsApproved(string? key)
    {
        if (!IsKey(key)) return false;

        lock (_gate)
        {
            return _byKey.TryGetValue(key!, out var entry) && entry.Status == PhoneStatus.Approved;
        }
    }

    /// <summary>
    /// A phone asking to score. Asking again while waiting only updates the
    /// name, and a phone already allowed is simply told so. Comes back as
    /// pending, approved, or busy when the queue is full.
    /// </summary>
    public string Ask(string key, string? name)
    {
        var clean = Clean(name);
        string result;

        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var entry))
            {
                if (entry.Status == PhoneStatus.Approved) return "approved";
                entry.Name = clean;
                result = "pending";
            }
            else
            {
                if (_byKey.Values.Count(item => item.Status == PhoneStatus.Pending) >= MaxPending) return "busy";

                _byKey[key] = new Entry(key, ++_nextId, clean);
                result = "pending";
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <summary>Everyone the PC should list: waiting phones first, oldest first.</summary>
    public IReadOnlyList<ScoringPhone> List()
    {
        lock (_gate)
        {
            return
            [
                .. _byKey.Values
                    .OrderBy(entry => entry.Status == PhoneStatus.Pending ? 0 : 1)
                    .ThenBy(entry => entry.Id)
                    .Select(entry => new ScoringPhone(entry.Id, entry.Name, entry.Status))
            ];
        }
    }

    public void Approve(int id)
    {
        string? key = null;

        lock (_gate)
        {
            var entry = Find(id);
            if (entry is null || entry.Status != PhoneStatus.Pending) return;
            if (_byKey.Values.Count(item => item.Status == PhoneStatus.Approved) >= MaxApproved) return;

            entry.Status = PhoneStatus.Approved;
            key = entry.Key;
        }

        Raise(key, "approved");
    }

    /// <summary>Turns down a waiting phone. It can ask again.</summary>
    public void Deny(int id) => Remove(id, PhoneStatus.Pending, "denied");

    /// <summary>Takes scoring away from an allowed phone. Its buttons go at once.</summary>
    public void Revoke(int id) => Remove(id, PhoneStatus.Approved, "revoked");

    private void Remove(int id, PhoneStatus expected, string word)
    {
        string? key = null;

        lock (_gate)
        {
            var entry = Find(id);
            if (entry is null || entry.Status != expected) return;

            _byKey.Remove(entry.Key);
            key = entry.Key;
        }

        Raise(key, word);
    }

    private void Raise(string key, string word)
    {
        StatusChanged?.Invoke(this, new AccessChange(key, word));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Entry? Find(int id) => _byKey.Values.FirstOrDefault(entry => entry.Id == id);

    private static string Word(PhoneStatus status) =>
        status == PhoneStatus.Approved ? "approved" : "pending";

    /// <summary>Printable, one line, short enough for the narrow strip.</summary>
    private static string Clean(string? name)
    {
        var text = string.Concat((name ?? string.Empty).Where(ch => !char.IsControl(ch))).Trim();
        if (text.Length == 0) return "A phone";
        return text.Length <= MaxName ? text : text[..MaxName].TrimEnd();
    }
}
