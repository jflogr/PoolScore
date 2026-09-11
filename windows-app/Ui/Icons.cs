using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;

namespace PoolScoreTracker.Ui;

/// <summary>
/// Toolbar icons, as outlines rather than pictures.
///
/// The path data below is Material Symbols (Outlined, 24px), Apache 2.0, taken
/// verbatim from Google's own SVGs so the shapes are the real ones rather than
/// something hand-drawn that only approximates them. Keeping the path rather
/// than a bitmap means they render sharp at whatever size the toolbar asks for,
/// including on a scaled display.
///
/// Material's viewBox is "0 -960 960 960" - a 960 grid with y running from
/// -960 up to 0 - which is why rendering translates before it scales.
/// </summary>
public static class Icons
{
    private const float Grid = 960f;

    public const string Undo =
        "M280-200v-80h284q63 0 109.5-40T720-420q0-60-46.5-100T564-560H312l104 104-56 56-200-200 200-200 56 56-104 104h252q97 0 166.5 63T800-420q0 94-69.5 157T564-200H280Z";

    public const string SwapHoriz =
        "M280-160 80-360l200-200 56 57-103 103h287v80H233l103 103-56 57Zm400-240-56-57 103-103H440v-80h287L624-743l56-57 200 200-200 200Z";

    public const string Group =
        "M40-160v-112q0-34 17.5-62.5T104-378q62-31 126-46.5T360-440q66 0 130 15.5T616-378q29 15 46.5 43.5T680-272v112H40Zm720 0v-120q0-44-24.5-84.5T666-434q51 6 96 20.5t84 35.5q36 20 55 44.5t19 53.5v120H760ZM247-527q-47-47-47-113t47-113q47-47 113-47t113 47q47 47 47 113t-47 113q-47 47-113 47t-113-47Zm466 0q-47 47-113 47-11 0-28-2.5t-28-5.5q27-32 41.5-71t14.5-81q0-42-14.5-81T544-792q14-5 28-6.5t28-1.5q66 0 113 47t47 113q0 66-47 113ZM120-240h480v-32q0-11-5.5-20T580-306q-54-27-109-40.5T360-360q-56 0-111 13.5T140-306q-9 5-14.5 14t-5.5 20v32Zm296.5-343.5Q440-607 440-640t-23.5-56.5Q393-720 360-720t-56.5 23.5Q280-673 280-640t23.5 56.5Q327-560 360-560t56.5-23.5ZM360-240Zm0-400Z";

    public const string Tv =
        "M320-120v-80H160q-33 0-56.5-23.5T80-280v-480q0-33 23.5-56.5T160-840h640q33 0 56.5 23.5T880-760v480q0 33-23.5 56.5T800-200H640v80H320ZM160-280h640v-480H160v480Zm0 0v-480 480Z";

    public const string RestartAlt =
        "M440-122q-121-15-200.5-105.5T160-440q0-66 26-126.5T260-672l57 57q-38 34-57.5 79T240-440q0 88 56 155.5T440-202v80Zm80 0v-80q87-16 143.5-83T720-440q0-100-70-170t-170-70h-3l44 44-56 56-140-140 140-140 56 56-44 44h3q134 0 227 93t93 227q0 121-79.5 211.5T520-122Z";

    public const string AddCircle =
        "M440-280h80v-160h160v-80H520v-160h-80v160H280v80h160v160Zm40 200q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm0-80q134 0 227-93t93-227q0-134-93-227t-227-93q-134 0-227 93t-93 227q0 134 93 227t227 93Zm0-320Z";

    public const string Add =
        "M440-440H200v-80h240v-240h80v240h240v80H520v240h-80v-240Z";

    public const string Edit =
        "M200-200h57l391-391-57-57-391 391v57Zm-80 80v-170l528-527q12-11 26.5-17t30.5-6q16 0 31 6t26 18l55 56q12 11 17.5 26t5.5 30q0 16-5.5 30.5T817-647L290-120H120Zm640-584-56-56 56 56Zm-141 85-28-29 57 57-29-28Z";

    public const string Delete =
        "M280-120q-33 0-56.5-23.5T200-200v-520h-40v-80h200v-40h240v40h200v80h-40v520q0 33-23.5 56.5T680-120H280Zm400-600H280v520h400v-520ZM360-280h80v-360h-80v360Zm160 0h80v-360h-80v360ZM280-720v520-520Z";

    /// <summary>
    /// The app's own icon, so every window it opens carries it without each
    /// caller having to remember to pass it along.
    /// </summary>
    public static Icon? App { get; } = LoadApp();

    private static Icon? LoadApp()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("PoolScoreTracker.app.ico");

            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null; // the default window icon will do
        }
    }

    public static Bitmap Render(string pathData, int size, Color colour)
    {
        var bitmap = new Bitmap(size, size);

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var path = Parse(pathData);
        using var matrix = new Matrix();
        matrix.Scale(size / Grid, size / Grid);
        matrix.Translate(0, Grid);
        path.Transform(matrix);

        using var brush = new SolidBrush(colour);
        g.FillPath(brush, path);
        return bitmap;
    }

    /// <summary>
    /// Enough of the SVG path grammar for these icons: moves, lines, cubic and
    /// quadratic curves with their smooth forms, and close. Elliptical arcs are
    /// the one gap - none of the icons above use them, and an arc falls back to
    /// a straight line to its endpoint rather than throwing mid-paint.
    /// </summary>
    private static GraphicsPath Parse(string d)
    {
        // Nonzero winding, which is what SVG fills with; the default in GDI+ is
        // alternate, and the icons with rings in them come out hollow under it.
        var path = new GraphicsPath { FillMode = FillMode.Winding };

        var at = 0;
        var command = ' ';
        var previous = ' ';
        PointF current = default, subpathStart = default, control = default;

        while (true)
        {
            SkipSeparators(d, ref at);
            if (at >= d.Length) break;

            if (char.IsLetter(d[at]))
            {
                command = d[at];
                at++;
            }
            else if (command == ' ')
            {
                break; // numbers before any command: give up rather than guess
            }
            else if (command is 'M' or 'm')
            {
                // Repeated pairs after a move are line-tos, per the spec.
                command = command == 'M' ? 'L' : 'l';
            }

            var relative = char.IsLower(command);
            var upper = char.ToUpperInvariant(command);

            switch (upper)
            {
                case 'M':
                    current = Point(d, ref at, relative, current);
                    subpathStart = current;
                    path.StartFigure();
                    break;

                case 'L':
                {
                    var next = Point(d, ref at, relative, current);
                    path.AddLine(current, next);
                    current = next;
                    break;
                }

                case 'H':
                {
                    var x = Number(d, ref at);
                    var next = new PointF(relative ? current.X + x : x, current.Y);
                    path.AddLine(current, next);
                    current = next;
                    break;
                }

                case 'V':
                {
                    var y = Number(d, ref at);
                    var next = new PointF(current.X, relative ? current.Y + y : y);
                    path.AddLine(current, next);
                    current = next;
                    break;
                }

                case 'C':
                {
                    var c1 = Point(d, ref at, relative, current);
                    var c2 = Point(d, ref at, relative, current);
                    var end = Point(d, ref at, relative, current);
                    path.AddBezier(current, c1, c2, end);
                    control = c2;
                    current = end;
                    break;
                }

                case 'S':
                {
                    var c1 = char.ToUpperInvariant(previous) is 'C' or 'S' ? Reflect(control, current) : current;
                    var c2 = Point(d, ref at, relative, current);
                    var end = Point(d, ref at, relative, current);
                    path.AddBezier(current, c1, c2, end);
                    control = c2;
                    current = end;
                    break;
                }

                case 'Q':
                {
                    var q = Point(d, ref at, relative, current);
                    var end = Point(d, ref at, relative, current);
                    AddQuadratic(path, current, q, end);
                    control = q;
                    current = end;
                    break;
                }

                case 'T':
                {
                    var q = char.ToUpperInvariant(previous) is 'Q' or 'T' ? Reflect(control, current) : current;
                    var end = Point(d, ref at, relative, current);
                    AddQuadratic(path, current, q, end);
                    control = q;
                    current = end;
                    break;
                }

                case 'A':
                {
                    // Unsupported: skip the five flags and radii, straight to the end point.
                    for (var i = 0; i < 5; i++) Number(d, ref at);
                    var end = Point(d, ref at, relative, current);
                    path.AddLine(current, end);
                    current = end;
                    break;
                }

                case 'Z':
                    path.CloseFigure();
                    current = subpathStart;
                    break;

                default:
                    return path; // something we do not understand; stop here
            }

            previous = command;
        }

        return path;
    }

    /// <summary>A quadratic curve as the cubic GDI+ actually draws.</summary>
    private static void AddQuadratic(GraphicsPath path, PointF from, PointF q, PointF to)
    {
        var c1 = new PointF(from.X + 2f / 3f * (q.X - from.X), from.Y + 2f / 3f * (q.Y - from.Y));
        var c2 = new PointF(to.X + 2f / 3f * (q.X - to.X), to.Y + 2f / 3f * (q.Y - to.Y));
        path.AddBezier(from, c1, c2, to);
    }

    private static PointF Reflect(PointF control, PointF about) =>
        new(2 * about.X - control.X, 2 * about.Y - control.Y);

    private static PointF Point(string d, ref int at, bool relative, PointF current)
    {
        var x = Number(d, ref at);
        var y = Number(d, ref at);
        return relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
    }

    private static void SkipSeparators(string d, ref int at)
    {
        while (at < d.Length && (d[at] is ' ' or ',' or '\t' or '\r' or '\n')) at++;
    }

    private static float Number(string d, ref int at)
    {
        SkipSeparators(d, ref at);

        var start = at;
        if (at < d.Length && d[at] is '-' or '+') at++;

        while (at < d.Length && (char.IsDigit(d[at]) || d[at] == '.')) at++;

        // Exponent, for completeness - Material does not use it, but a path
        // copied from elsewhere might.
        if (at < d.Length && (d[at] == 'e' || d[at] == 'E'))
        {
            at++;
            if (at < d.Length && d[at] is '-' or '+') at++;
            while (at < d.Length && char.IsDigit(d[at])) at++;
        }

        return start == at
            ? 0f
            : float.Parse(d.AsSpan(start, at - start), CultureInfo.InvariantCulture);
    }
}
