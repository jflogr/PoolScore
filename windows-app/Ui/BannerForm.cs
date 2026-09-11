using System.ComponentModel;
using System.Drawing.Drawing2D;
using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Ui;

/// <summary>
/// What the stream banner shows. Names arrive ready to display, one entry per
/// player, so the banner can decide whether they share a line or take one each.
/// </summary>
public sealed record BannerState(
    string[] HomeNames,
    string[] AwayNames,
    int HomeScore,
    int AwayScore,
    int HomeTarget,
    int AwayTarget,
    Side? Breaking)
{
    public static readonly BannerState Blank = new([], [], 0, 0, 3, 3, null);

    /// <summary>The race, when both sides are chasing the same one.</summary>
    public int? Race => HomeTarget == AwayTarget ? HomeTarget : null;

    public bool HomeWon => HomeScore >= HomeTarget;
    public bool AwayWon => AwayScore >= AwayTarget;
    public bool HomeOnHill => !HomeWon && HomeScore == HomeTarget - 1;
    public bool AwayOnHill => !AwayWon && AwayScore == AwayTarget - 1;
}

/// <summary>
/// The window OBS captures: fixed size, no scoring controls, nothing but the
/// score line. Drawn with GDI+ on purpose - a GDI+ paint lands in the window's
/// redirection bitmap, so every capture method works, on any Windows version,
/// including OBS's BitBlt and while the window is buried behind others. That is
/// the whole reason this is not another WebView.
/// </summary>
public sealed class BannerForm : Form
{
    // Deliberately NOT FixedToolWindow: that sets WS_EX_TOOLWINDOW, and OBS
    // drops tool windows from its window list entirely.
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 120;

    private static readonly Color Ink = ColorTranslator.FromHtml("#2b3846");

    /// <summary>The scores sit on their own darker block, the way a TV graphic does.</summary>
    private static readonly Color Block = ColorTranslator.FromHtml("#16202b");

    private static readonly Color Paper = Color.White;
    private static readonly Color Muted = ColorTranslator.FromHtml("#8ea0b4");
    private static readonly Color Empty = ColorTranslator.FromHtml("#33445a");
    private static readonly Color Hill = ColorTranslator.FromHtml("#d9a441");

    private BannerState _state = BannerState.Blank;

    public BannerForm()
    {
        Text = "Pool Score Banner";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(DefaultWidth, DefaultHeight);
        BackColor = Ink;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BannerState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            Invalidate();
        }
    }

    /// <summary>Exact pixel size for the capture, so OBS needs no cropping.</summary>
    public void SetCaptureSize(int width, int height) => ClientSize = new Size(width, height);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Ink);

        var height = ClientSize.Height;
        var width = ClientSize.Width;
        if (width < 200 || height < 50) return;

        var blockHeight = height * 0.76f;
        var blockTop = (height - blockHeight) / 2f;
        var centre = height / 2f;

        var gap = height * 0.10f;
        var pad = height * 0.16f;
        var barWidth = height * 0.85f;
        var barHeight = height * 0.155f;
        var dot = (int)(height * 0.11);
        var dotSpace = dot + gap / 2f;

        using var numberFont = new Font(FontFamily.GenericSansSerif, blockHeight * 0.56f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var raceFont = new Font(FontFamily.GenericSansSerif, blockHeight * 0.24f, FontStyle.Bold, GraphicsUnit.Pixel);

        // Both scores get the same width so the block does not jump about when
        // a single digit becomes two.
        var numberWidth = Math.Max(
            g.MeasureString(_state.HomeScore.ToString(), numberFont).Width,
            g.MeasureString(_state.AwayScore.ToString(), numberFont).Width);
        numberWidth = Math.Max(numberWidth, g.MeasureString("0", numberFont).Width * 1.2f);

        var race = _state.Race is int target ? $"({target})" : string.Empty;
        var raceWidth = race.Length > 0 ? g.MeasureString(race, raceFont).Width : 0f;

        // bar | score | (race) | score | bar
        var parts = race.Length > 0
            ? new[] { barWidth, numberWidth, raceWidth, numberWidth, barWidth }
            : [barWidth, numberWidth, numberWidth, barWidth];

        var blockWidth = pad * 2 + parts.Sum() + gap * (parts.Length - 1);
        var blockLeft = (width - blockWidth) / 2f;

        using (var path = RoundedRect(new RectangleF(blockLeft, blockTop, blockWidth, blockHeight), blockHeight * 0.16f))
        using (var brush = new SolidBrush(Block))
        {
            g.FillPath(brush, path);
        }

        var x = blockLeft + pad;

        DrawBar(g, new RectangleF(x, centre - barHeight / 2f, barWidth, barHeight),
                _state.HomeScore, _state.HomeTarget, _state.HomeOnHill || _state.HomeWon);
        x += barWidth + gap;

        DrawNumber(g, _state.HomeScore, numberFont, x, numberWidth, centre,
                   _state.HomeOnHill || _state.HomeWon);
        x += numberWidth + gap;

        if (race.Length > 0)
        {
            var size = g.MeasureString(race, raceFont);
            using var muted = new SolidBrush(Muted);
            g.DrawString(race, raceFont, muted, x, centre - size.Height / 2f);
            x += raceWidth + gap;
        }

        DrawNumber(g, _state.AwayScore, numberFont, x, numberWidth, centre,
                   _state.AwayOnHill || _state.AwayWon);
        x += numberWidth + gap;

        DrawBar(g, new RectangleF(x, centre - barHeight / 2f, barWidth, barHeight),
                _state.AwayScore, _state.AwayTarget, _state.AwayOnHill || _state.AwayWon);

        // Names take whatever is left either side of the block, less a margin
        // so a long one does not run right up to the edge of the capture.
        var sideWidth = Math.Max(0f, blockLeft - gap - dotSpace - pad);
        var nameBox = new SizeF(sideWidth, height - gap);
        var nameSize = Math.Min(
            Measure(g, _state.HomeNames, height * 0.34f, nameBox),
            Measure(g, _state.AwayNames, height * 0.34f, nameBox));

        DrawName(g, _state.HomeNames, nameSize, nameBox, blockLeft - gap, centre,
                 rightAligned: true, breaking: _state.Breaking == Side.Home, dot, dotSpace);

        DrawName(g, _state.AwayNames, nameSize, nameBox, blockLeft + blockWidth + gap, centre,
                 rightAligned: false, breaking: _state.Breaking == Side.Away, dot, dotSpace);
    }

    private static void DrawNumber(Graphics g, int score, Font font, float left, float width, float centre, bool lit)
    {
        var text = score.ToString();
        var size = g.MeasureString(text, font);
        using var brush = new SolidBrush(lit ? Hill : Paper);
        g.DrawString(text, font, brush, left + (width - size.Width) / 2f, centre - size.Height / 2f);
    }

    /// <summary>
    /// One segment per rack in that side's race, filling as they are won. With
    /// a handicap the two bars hold different numbers of segments, so what
    /// reads across is how full each one is.
    /// </summary>
    private static void DrawBar(Graphics g, RectangleF box, int score, int target, bool lit)
    {
        if (target < 1) return;

        var gap = Math.Max(1.5f, box.Width / (target * 14f));
        var segment = (box.Width - gap * (target - 1)) / target;
        var radius = Math.Min(segment, box.Height) * 0.28f;

        for (var i = 0; i < target; i++)
        {
            var cell = new RectangleF(box.Left + i * (segment + gap), box.Top, segment, box.Height);
            using var path = RoundedRect(cell, radius);
            using var brush = new SolidBrush(i < score ? lit ? Hill : Paper : Empty);
            g.FillPath(brush, path);
        }
    }

    /// <summary>Largest font size at which this side's names fit the box.</summary>
    private static float Measure(Graphics g, string[] names, float preferred, SizeF box)
    {
        using var block = NameLayout.Fit(g, names, " | ", preferred, 10f, box, FontStyle.Bold);
        return block.Font.Size;
    }

    private static void DrawName(Graphics g, string[] names, float fontSize, SizeF box,
                                 float edge, float centre,
                                 bool rightAligned, bool breaking, int dot, float dotSpace)
    {
        using var block = NameLayout.At(g, names, " | ", fontSize, box.Width, FontStyle.Bold);

        var area = new RectangleF(
            rightAligned ? edge - dotSpace - box.Width : edge + dotSpace,
            centre - box.Height / 2f,
            box.Width,
            box.Height);

        NameLayout.Draw(g, block, area, rightAligned ? NameAlign.Right : NameAlign.Left, Paper);

        if (!breaking) return;

        using var gold = new SolidBrush(Hill);
        var dotX = rightAligned ? edge - dot : edge;
        g.FillEllipse(gold, dotX, centre - dot / 2f, dot, dot);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = Math.Max(1f, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
