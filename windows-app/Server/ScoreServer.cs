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

namespace PoolScoreTracker.Server;

/// <summary>
/// Serves the spectator view to phones on the same wifi. Read-only by design:
/// there is no endpoint that changes anything, so a phone cannot break a match
/// and there is no conflict handling to get wrong.
///
/// The live session belongs to the UI thread and is never touched from here.
/// The window hands over a finished <see cref="Snapshot"/> whenever something
/// changes, and requests are answered from that.
/// </summary>
public sealed class ScoreServer : IAsyncDisposable
{
    /// <summary>Frames a slow phone may fall behind before it starts losing them.</summary>
    private const int Backlog = 4;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _gate = new();
    private readonly List<Channel<string>> _listeners = [];

    private WebApplication? _app;
    private volatile string _payload = JsonSerializer.Serialize(Snapshot.Waiting, Json);
    private long _rev;

    public int Port { get; private set; }

    /// <summary>Addresses a phone on the same wifi can actually reach.</summary>
    public IReadOnlyList<string> Addresses { get; private set; } = [];

    public bool Running => _app is not null;

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

            // Anything else is the page again, so a stray path still works - but
            // only for a read. Left unguarded the fallback answers every verb,
            // and a POST coming back 200 would make a read-only server look
            // like it had accepted something.
            app.MapFallback((HttpContext context) =>
                HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                    ? Results.Content(Page(), "text/html; charset=utf-8")
                    : Results.StatusCode(StatusCodes.Status405MethodNotAllowed));

            await app.StartAsync();

            _app = app;
            Port = port;
            Addresses = [.. LanAddresses().Select(address => $"http://{address}:{port}/")];
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

        lock (_gate)
        {
            foreach (var listener in _listeners) listener.Writer.TryComplete();
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

        lock (_gate)
        {
            foreach (var listener in _listeners) listener.Writer.TryWrite(payload);
        }
    }

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

        lock (_gate) _listeners.Add(channel);

        try
        {
            await Write(context, _payload);

            // A comment every so often keeps the connection alive through a
            // phone dozing off or a router with an idle timeout.
            var keepAlive = Task.Delay(Timeout.Infinite, context.RequestAborted);

            while (!context.RequestAborted.IsCancellationRequested)
            {
                var next = channel.Reader.ReadAsync(context.RequestAborted).AsTask();
                var tick = Task.Delay(TimeSpan.FromSeconds(20), context.RequestAborted);

                var done = await Task.WhenAny(next, tick, keepAlive);
                if (done == keepAlive) break;

                if (done == next) await Write(context, await next);
                else await context.Response.WriteAsync(": ping\n\n", context.RequestAborted);

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
            lock (_gate) _listeners.Remove(channel);
        }
    }

    private static async Task Write(HttpContext context, string payload)
    {
        await context.Response.WriteAsync($"event: state\ndata: {payload}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    private static string Page()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("web/phone.html");
        if (stream is null) return "<h1>Pool Score Tracker</h1><p>The phone view is missing from this build.</p>";

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Home and office ranges first, so the address offered to a phone is one
    /// it can actually reach - Hyper-V and WSL both hand out 172.x addresses
    /// that go nowhere useful.
    /// </summary>
    private static IEnumerable<string> LanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address)
            .Where(address => !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())
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
