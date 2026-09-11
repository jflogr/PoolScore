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
        Application.Run(new MainWindow(session) { Icon = Icons.App });
    }
}
