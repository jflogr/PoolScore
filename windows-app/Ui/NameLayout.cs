namespace PoolScoreTracker.Ui;

public enum NameAlign
{
    Left,
    Centre,
    Right
}

public enum NameVAlign
{
    Top,
    Centre
}

/// <summary>
/// A block of names laid out to fill a box: the chosen font, and the lines to
/// draw. Owns the font, so dispose it.
/// </summary>
public sealed class NameBlock(Font font, string[] lines, float lineHeight) : IDisposable
{
    public Font Font { get; } = font;
    public string[] Lines { get; } = lines;
    public float LineHeight { get; } = lineHeight;
    public float TotalHeight => Lines.Length * LineHeight;

    public void Dispose() => Font.Dispose();
}

/// <summary>
/// Fitting names into a box. Shrinking a long team name until it fits on one
/// line makes it unreadable, which is the thing we are trying to avoid, so
/// names wrap onto as many lines as the box allows and only then get smaller.
///
/// Breaking inside somebody's name is a last resort though: a line breaks
/// between two players first, and only splits a single name across lines when
/// doing so buys a real gain in size. Otherwise "PLAYER 1" would helpfully
/// arrange itself as "PLAYER" over "1".
/// </summary>
public static class NameLayout
{
    /// <summary>
    /// How much bigger breaking a name across lines has to make the text before
    /// it is worth doing. Set high on purpose: "DAVE SM" split over two lines
    /// reads worse than "DAVE SM" a bit smaller, and only a genuinely long team
    /// name gains enough to justify it.
    /// </summary>
    private const float WrapWorthIt = 1.6f;

    /// <summary>
    /// Best layout for these names: keeps each name whole where that is
    /// legible, and splits one across lines only when that is a real gain.
    /// </summary>
    public static NameBlock Fit(
        Graphics g,
        IReadOnlyList<string> names,
        string? separator,
        float maxSize,
        float minSize,
        SizeF box,
        FontStyle style)
    {
        var whole = FitAt(g, names, separator, maxSize, minSize, box, style, allowWordWrap: false);

        // Already as big as we wanted - nothing to gain by breaking names up.
        if (whole.Font.Size >= maxSize - 0.01f) return whole;

        var wrapped = FitAt(g, names, separator, maxSize, minSize, box, style, allowWordWrap: true);

        if (wrapped.Font.Size > whole.Font.Size * WrapWorthIt)
        {
            whole.Dispose();
            return wrapped;
        }

        wrapped.Dispose();
        return whole;
    }

    /// <summary>
    /// Lines for these names at a size decided elsewhere - used where two
    /// blocks have to share one size so they do not look lopsided. Keeps names
    /// whole if they fit at that size, and breaks them up only if they do not.
    /// </summary>
    public static NameBlock At(
        Graphics g,
        IReadOnlyList<string> names,
        string? separator,
        float size,
        float maxWidth,
        FontStyle style)
    {
        var font = new Font(FontFamily.GenericSansSerif, Math.Max(1f, size), style, GraphicsUnit.Pixel);

        var plain = BuildLines(g, names, separator, font, maxWidth, allowWordWrap: false);
        if (plain.All(line => g.MeasureString(line, font).Width <= maxWidth))
        {
            return new NameBlock(font, [.. plain], font.GetHeight(g));
        }

        var wrapped = BuildLines(g, names, separator, font, maxWidth, allowWordWrap: true);
        return new NameBlock(font, [.. wrapped], font.GetHeight(g));
    }

    private static NameBlock FitAt(
        Graphics g,
        IReadOnlyList<string> names,
        string? separator,
        float maxSize,
        float minSize,
        SizeF box,
        FontStyle style,
        bool allowWordWrap)
    {
        if (names.Count == 0)
        {
            var none = new Font(FontFamily.GenericSansSerif, Math.Max(1f, minSize), style, GraphicsUnit.Pixel);
            return new NameBlock(none, [], none.GetHeight(g));
        }

        for (var size = maxSize; size > minSize; size -= 1f)
        {
            var candidate = new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
            var lines = BuildLines(g, names, separator, candidate, box.Width, allowWordWrap);
            var lineHeight = candidate.GetHeight(g);

            var fitsWidth = lines.All(line => g.MeasureString(line, candidate).Width <= box.Width);
            var fitsHeight = lines.Count * lineHeight <= box.Height;

            if (fitsWidth && fitsHeight) return new NameBlock(candidate, [.. lines], lineHeight);

            candidate.Dispose();
        }

        // Nothing fit cleanly - take the smallest and let it be tight.
        var smallest = new Font(FontFamily.GenericSansSerif, Math.Max(1f, minSize), style, GraphicsUnit.Pixel);
        var last = BuildLines(g, names, separator, smallest, box.Width, allowWordWrap);
        return new NameBlock(smallest, [.. last], smallest.GetHeight(g));
    }

    private static List<string> BuildLines(
        Graphics g, IReadOnlyList<string> names, string? separator, Font font, float maxWidth, bool allowWordWrap)
    {
        var lines = new List<string>();

        if (separator is null)
        {
            // One player per line.
            foreach (var name in names)
            {
                if (allowWordWrap) lines.AddRange(WrapWords(g, name, font, maxWidth));
                else lines.Add(name);
            }

            return lines;
        }

        var current = string.Empty;

        foreach (var name in names)
        {
            var candidate = current.Length == 0 ? name : current + separator + name;

            if (g.MeasureString(candidate, font).Width <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }

            if (!allowWordWrap || g.MeasureString(name, font).Width <= maxWidth)
            {
                current = name;
                continue;
            }

            // This name will not fit a line on its own, so break it up and
            // carry the tail forward in case the next name joins it.
            var wrapped = WrapWords(g, name, font, maxWidth);
            lines.AddRange(wrapped.Take(wrapped.Count - 1));
            current = wrapped[^1];
        }

        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    /// <summary>
    /// Shorter than this and a name is never broken up, however much room that
    /// would buy. "DAVE SM" split over two lines is not a bigger name, it is
    /// two things that look like two players.
    /// </summary>
    private const int ShortestWrappable = 14;

    private static List<string> WrapWords(Graphics g, string text, Font font, float maxWidth)
    {
        if (text.Length < ShortestWrappable) return [text];

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [string.Empty];

        var lines = new List<string>();
        var current = words[0];

        foreach (var word in words.Skip(1))
        {
            var candidate = current + " " + word;
            if (g.MeasureString(candidate, font).Width <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        lines.Add(current);
        return lines;
    }

    public static void Draw(Graphics g, NameBlock block, RectangleF box, NameAlign align, Color colour,
                            NameVAlign vertical = NameVAlign.Centre)
    {
        if (block.Lines.Length == 0) return;

        using var brush = new SolidBrush(colour);
        var top = vertical == NameVAlign.Top
            ? box.Top
            : box.Top + (box.Height - block.TotalHeight) / 2f;

        for (var i = 0; i < block.Lines.Length; i++)
        {
            var line = block.Lines[i];
            var width = g.MeasureString(line, block.Font).Width;

            var x = align switch
            {
                NameAlign.Right => box.Right - width,
                NameAlign.Centre => box.Left + (box.Width - width) / 2f,
                _ => box.Left
            };

            g.DrawString(line, block.Font, brush, x, top + i * block.LineHeight);
        }
    }
}
