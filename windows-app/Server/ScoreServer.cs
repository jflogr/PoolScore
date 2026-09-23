using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Server;

/// <summary>A phone asking for one rack to be added to or taken off a side.</summary>
public sealed record PhoneTap(string Game, Side Side, int Delta);

/// <summary>
/// Serves the phone view to phones on the same wifi. Every phone can watch.
/// A phone can also ask to score, and once somebody at the PC allows it, it
/// can nudge the current score up or down by one rack - and nothing else: no
/// format, players, finishing a set or undo, so the setup stays safe.
/// <see cref="Phones"/> keeps track of who is allowed.
///
/// The live session belongs to the UI thread and is never touched from here.
/// The window hands over a finished <see cref="Snapshot"/> whenever something
/// changes, and requests are answered from that. A tap goes the other way as
/// a <see cref="Tapped"/> event, and the window applies it on its own thread.
/// </summary>
public sealed class ScoreServer : IAsyncDisposable
{
    /// <summary>Frames a slow phone may fall behind before it starts losing them.</summary>
    private const int Backlog = 4;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>One open event stream, and the key of the phone on the end of it, if it gave one.</summary>
    private sealed record Listener(Channel<string> Channel, string? Key);

    private readonly Lock _gate = new();
    private readonly List<Listener> _listeners = [];

    private WebApplication? _app;
    private volatile string _payload = JsonSerializer.Serialize(Snapshot.Waiting, Json);
    private long _rev;

    public int Port { get; private set; }

    /// <summary>Addresses a phone on the same wifi can actually reach.</summary>
    public IReadOnlyList<string> Addresses { get; private set; } = [];

    public bool Running => _app is not null;

    /// <summary>Which phones may score. Lives as long as the app does.</summary>
    public PhoneAccess Phones { get; } = new();

    public ScoreServer()
    {
        // Tell the phone concerned straight away, rather than leaving it to
        // find out on its next tap.
        Phones.StatusChanged += (_, change) =>
        {
            var frame = Frame("access", Access(change.Status));

            lock (_gate)
            {
                foreach (var listener in _listeners.Where(item => item.Key == change.Key))
                {
                    listener.Channel.Writer.TryWrite(frame);
                }
            }
        };
    }

    /// <summary>
    /// Raised when the addresses a phone could use have changed - a laptop that
    /// joined the wifi after the app opened, or picked up a different lease
    /// part way through the night. Arrives on a background thread.
    /// </summary>
    public event EventHandler? AddressesChanged;

    /// <summary>
    /// A phone tapped plus or minus. Arrives on a request thread; the window
    /// decides whether it still applies and makes the change on the UI thread.
    /// </summary>
    public event EventHandler<PhoneTap>? Tapped;

    public async Task<bool> StartAsync(int port = 4174)
    {
        if (_app is not null) return true;

        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(port));

            var app = builder.Build();

            app.MapGet("/", () => Results.Content(Page(), "text/html; charset=utf-8"));
            app.MapGet("/api/state", () => Results.Content(_payload, "application/json"));
            app.MapGet("/api/events", Events);
            app.MapGet("/api/access", (HttpContext context) =>
                Results.Content(Access(Phones.StatusOf(context.Request.Query["key"])), "application/json"));
            // Cast to Delegate, or these bind as a plain RequestDelegate and
            // the result they return - the ask's answer, a tap's 403 - is
            // thrown away, leaving every reply an empty 200.
            app.MapPost("/api/ask", (Delegate)Ask);
            app.MapPost("/api/tap", (Delegate)Tap);

            // Anything else is the page again, so a stray path still works - but
            // only for a read. Left unguarded the fallback answers every verb,
            // and a POST coming back 200 would look like it had been accepted.
            app.MapFallback((HttpContext context) =>
                HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                    ? Results.Content(Page(), "text/html; charset=utf-8")
                    : Results.StatusCode(StatusCodes.Status405MethodNotAllowed));

            await app.StartAsync();

            _app = app;
            Port = port;
            Addresses = Reachable();

            // Kestrel is listening on every address the machine has, including
            // the ones it does not have yet, so a late-joining network needs
            // nothing reopened - only the address on screen brought up to date.
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            return true;
        }
        catch (Exception)
        {
            // Almost always the port already being in use. The app is perfectly
            // usable without phone sharing, so this is not worth failing over.
            _app = null;
            return false;
        }
    }

    public async Task StopAsync()
    {
        var app = _app;
        _app = null;
        if (app is null) return;

        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;

        lock (_gate)
        {
            foreach (var listener in _listeners) listener.Channel.Writer.TryComplete();
            _listeners.Clear();
        }

        await app.StopAsync(TimeSpan.FromSeconds(2));
        await app.DisposeAsync();
    }

    /// <summary>
    /// Called from the UI thread with a fresh snapshot. Never blocks: each
    /// phone has its own small queue and a phone that cannot keep up drops
    /// frames rather than holding up the scorer.
    /// </summary>
    public void Publish(Snapshot snapshot)
    {
        var payload = JsonSerializer.Serialize(snapshot with { Rev = ++_rev }, Json);
        _payload = payload;

        var frame = Frame("state", payload);

        lock (_gate)
        {
            foreach (var listener in _listeners) listener.Channel.Writer.TryWrite(frame);
        }
    }

    /// <summary>
    /// A phone asking to score. Files it for the PC to allow or turn down;
    /// the phone hears the answer on its event stream.
    /// </summary>
    private async Task<IResult> Ask(HttpContext context)
    {
        AskRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<AskRequest>(Json, context.RequestAborted);
        }
        catch (Exception)
        {
            return Results.BadRequest();
        }

        if (request is null || !PhoneAccess.IsKey(request.Key)) return Results.BadRequest();

        return Results.Content(Access(Phones.Ask(request.Key!, request.Name)), "application/json");
    }

    private sealed record AskRequest(string? Key, string? Name);

    /// <summary>
    /// One rack up or down for one side, from a phone the PC has allowed.
    /// Answered as soon as it is handed on:
    /// the phone learns the result the same way it learns everything else,
    /// from the next snapshot on the event stream.
    /// </summary>
    private async Task<IResult> Tap(HttpContext context)
    {
        TapRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<TapRequest>(Json, context.RequestAborted);
        }
        catch (Exception)
        {
            return Results.BadRequest();
        }

        if (request is null || string.IsNullOrEmpty(request.Game)) return Results.BadRequest();
        if (request.Delta is not (1 or -1)) return Results.BadRequest();

        // Watching is open to anyone who scans the code; scoring is not.
        if (!Phones.IsApproved(request.Key)) return Results.StatusCode(StatusCodes.Status403Forbidden);

        Side side;
        if (request.Side == "home") side = Side.Home;
        else if (request.Side == "away") side = Side.Away;
        else return Results.BadRequest();

        Tapped?.Invoke(this, new PhoneTap(request.Game, side, request.Delta));
        return Results.Accepted();
    }

    private sealed record TapRequest(string? Key, string? Game, string? Side, int Delta);

    private async Task Events(HttpContext context)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-transform";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Backlog)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        string? key = context.Request.Query["key"];
        if (!PhoneAccess.IsKey(key)) key = null;

        var listener = new Listener(channel, key);
        lock (_gate) _listeners.Add(listener);

        try
        {
            await Write(context, Frame("state", _payload));

            // A phone that reconnects - after dozing, or after the app was
            // restarted and forgot every approval - learns where it stands.
            if (key is not null) await Write(context, Frame("access", Access(Phones.StatusOf(key))));

            // A ping every so often keeps the connection alive through a router
            // with an idle timeout. It is a named event rather than an SSE
            // comment because the page never sees comments, and the page needs
            // to hear something regularly to notice a connection that has died
            // without saying so.
            var keepAlive = Task.Delay(Timeout.Infinite, context.RequestAborted);

            while (!context.RequestAborted.IsCancellationRequested)
            {
                var next = channel.Reader.ReadAsync(context.RequestAborted).AsTask();
                var tick = Task.Delay(TimeSpan.FromSeconds(15), context.RequestAborted);

                var done = await Task.WhenAny(next, tick, keepAlive);
                if (done == keepAlive) break;

                if (done == next) await Write(context, await next);
                else await context.Response.WriteAsync(Frame("ping", "{}"), context.RequestAborted);

                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // the phone navigated away or the wifi dropped
        }
        catch (ChannelClosedException)
        {
            // the server is shutting down
        }
        finally
        {
            lock (_gate) _listeners.Remove(listener);
        }
    }

    private static string Frame(string name, string payload) => $"event: {name}\ndata: {payload}\n\n";

    private static string Access(string status) => JsonSerializer.Serialize(new { status }, Json);

    private static async Task Write(HttpContext context, string frame)
    {
        await context.Response.WriteAsync(frame, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    private static string Page()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("web/phone.html");
        if (stream is null) return "<h1>Pool Score</h1><p>The phone view is missing from this build.</p>";

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        if (_app is null) return;

        var refreshed = Reachable();
        if (refreshed.SequenceEqual(Addresses)) return;

        Addresses = refreshed;
        AddressesChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<string> Reachable() =>
        [.. LanAddresses().Select(address => $"http://{address}:{Port}/")];

    /// <summary>
    /// Addresses a phone on the same wifi could actually open, best first.
    ///
    /// The hard part is not finding addresses but discarding the ones that only
    /// look right. Hyper-V, WSL, VirtualBox, Docker and a VPN client all add
    /// adapters carrying perfectly ordinary private addresses that no phone can
    /// reach, and picking one of those fails in the worst possible way: the
    /// address on screen looks exactly like a good one, so the fault looks like
    /// the phone, the wifi, or the QR code.
    /// </summary>
    private static IEnumerable<string> LanAddresses()
    {
        var live = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType is not
                (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(nic => nic.GetIPProperties())
            .ToList();

        // An adapter with a gateway is one that leads somewhere. Virtual
        // switches and host-only adapters have none, which sorts them out
        // without having to recognise every product by name.
        var routed = live
            .Where(properties => properties.GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork
                && !gateway.Address.Equals(IPAddress.Any)))
            .ToList();

        // Falling back rather than showing nothing: an unusual setup with no
        // gateway at all is still better served by a guess than by silence.
        return Ordered(routed.Count > 0 ? routed : live);
    }

    /// <summary>Home and office ranges first, since that is where a match is.</summary>
    private static IEnumerable<string> Ordered(IEnumerable<IPInterfaceProperties> interfaces) =>
        interfaces
            .SelectMany(properties => properties.UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address)
            .Where(address => !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())

            // 169.254.x.x means no DHCP server ever answered. The machine has
            // an address, but it shares that network with nothing.
            .Where(address => !address.StartsWith("169.254.", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(Rank)
            .ThenBy(address => address, StringComparer.Ordinal);

    private static int Rank(string address)
    {
        if (address.StartsWith("192.168.", StringComparison.Ordinal)) return 0;
        if (address.StartsWith("10.", StringComparison.Ordinal)) return 1;

        var parts = address.Split('.');
        if (parts.Length == 4 && parts[0] == "172" && int.TryParse(parts[1], out var second)
            && second is >= 16 and <= 31)
        {
            return 2;
        }

        return 3;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
