using System.Reflection;
using PoolScoreTracker.Domain;
using PoolScoreTracker.Ui;

namespace PoolScoreTracker;

/// <summary>
/// A native scoreboard for pool matches. The window scores the game, a separate
/// fixed-size banner window is what OBS captures, and a small self-hosted server
/// gives phones on the same wifi a read-only view.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var session = SessionStore.Load();
        var window = new MainWindow(session);

        var icon = LoadIcon();
        if (icon is not null) window.Icon = icon;

        Application.Run(window);
    }

    private static Icon? LoadIcon()
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PoolScoreTracker.app.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null; // the default window icon will do
        }
    }
}
