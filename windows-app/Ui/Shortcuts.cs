using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Ui;

/// <summary>
/// The left and right arrows score the left and right sides; hold Alt to take
/// a rack off. Arrows rather than letters because they point at the side
/// they score, so nobody has to remember which letter is which.
///
/// Shared rather than owned by the main window, because a keystroke only ever
/// reaches the window that has focus. Click the banner once - to drag it onto
/// the right monitor, say - and the keys would go quiet with nothing on screen
/// to explain why, which is not something anyone diagnoses mid-match.
/// </summary>
internal static class Shortcuts
{
    /// <summary>The side and the change this key asks for, or null if it is not ours.</summary>
    public static (Side Side, int Delta)? Read(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (key is not (Keys.Left or Keys.Right)) return null;

        // Ctrl and Shift belong to whatever else might be listening for them.
        if ((keyData & (Keys.Control | Keys.Shift)) != 0) return null;

        var delta = (keyData & Keys.Alt) == Keys.Alt ? -1 : +1;
        return (key == Keys.Left ? Side.Home : Side.Away, delta);
    }

    /// <summary>Enter on its own: a miss, so the turn passes to the next player in the order.</summary>
    public static bool IsTurnOver(Keys keyData) => keyData == Keys.Enter;
}
