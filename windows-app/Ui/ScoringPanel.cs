using System.ComponentModel;
using System.Drawing.Drawing2D;
using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Ui;

/// <summary>What the scoring area shows. Names arrive ready to display, one per line.</summary>
public sealed record ScoringState(
    string[] HomeNames,
    string[] AwayNames,
    int HomeScore,
    int AwayScore,
    int HomeTarget,
    int AwayTarget,
    Side? Breaking)
{
    public static readonly ScoringState Blank = new([], [], 0, 0, 3, 3, null);

    public bool HomeWon => HomeScore >= HomeTarget;
    public bool AwayWon => AwayScore >= AwayTarget;

    /// <summary>
    /// Exactly one side is over the line. Both being over it is a tie nobody
    /// has won, which can happen if the scores are nudged past the race.
    /// </summary>
    public bool Decided => HomeWon ^ AwayWon;

    /// <summary>One rack away from taking it.</summary>
    public bool HomeOnHill => !HomeWon && HomeScore == HomeTarget - 1;
    public bool AwayOnHill => !AwayWon && AwayScore == AwayTarget - 1;
}

/// <summary>
/// The scoring area, filling the top half of the window. Everything here is
/// sized to be read from across the room: the names, then the score under them
/// as large as the space allows, with the two race bars running the full height
/// down the middle.
///
/// The bars are always the same height and sit side by side, so what you compare
/// is how full each one is. That stays honest when the sides race to different
/// totals on a handicap - a race to 3 gets three tall boxes, a race to 5 gets
/// five shorter ones, and the fuller bar really is the closer side.
/// </summary>
public sealed class ScoringPanel : Control
{
    private static readonly Color Ink = ColorTranslator.FromHtml("#2b3846");
    private static readonly Color Paper = Color.White;
    private static readonly Color Unwon = ColorTranslator.FromHtml("#3d4c5c");

    /// <summary>Halfway from white to gold: nearly there, but not there.</summary>
    private static readonly Color Hill = ColorTranslator.FromHtml("#ecd2a0");

    private static readonly Color Won = ColorTranslator.FromHtml("#d9a441");
    private static readonly Color ButtonFace = ColorTranslator.FromHtml("#3d4c5c");
    private static readonly Color ButtonHover = ColorTranslator.FromHtml("#4d5f73");
    private static readonly Color Hint = ColorTranslator.FromHtml("#8ea0b4");
    private static readonly Color ActionHover = ColorTranslator.FromHtml("#e8bd67");

    private ScoringState _state = ScoringState.Blank;
    private string _actionText = "Next match";
    private Rectangle _homeMinus, _homePlus, _awayMinus, _awayPlus, _action;
    private Rectangle? _hot;

    public event EventHandler<int>? HomeDelta;
    public event EventHandler<int>? AwayDelta;

    /// <summary>The centre button, shown only once a side has won.</summary>
    public event EventHandler? ActionClicked;

    public ScoringPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Ink;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ScoringState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            Invalidate();
        }
    }

    /// <summary>What the centre button says when a side has won.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ActionText
    {
        get => _actionText;
        set
        {
            if (_actionText == value) return;
            _actionText = value;
            Invalidate();
        }
    }

    /// <summary>White, part-gold on the hill, gold once it is won.</summary>
    private static Color Shade(bool won, bool onHill) => won ? Won : onHill ? Hill : Paper;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Grey anti-aliasing rather than ClearType: subpixel rendering puts
        // colour fringes on the edges, which at this size reads as a ragged,
        // pixelated glyph instead of a crisp one.
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Ink);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        if (w < 200 || h < 150) return;

        var pad = (int)(h * 0.05);

        // The bars are narrow now, so the two sides get almost the whole width
        // for their names and scores.
        var barArea = (int)(w * 0.11);
        var columnWidth = (w - barArea) / 2;

        // The buttons only have to be easy to tap on a laptop touchscreen, not
        // readable from across the room.
        var buttonHeight = Math.Clamp((int)(h * 0.20), 54, 84);
        var buttonTop = h - pad - buttonHeight;

        var nameHeight = (int)(h * 0.26);
        var numberTop = pad + nameHeight;
        var numberHeight = buttonTop - numberTop - pad / 2;

        // A line of its own under each score for the race. Taken out of the
        // number's share rather than the buttons', which have to stay big
        // enough to tap on a laptop touchscreen.
        var raceHeight = Math.Clamp((int)(numberHeight * 0.14f), 13, 30);
        numberHeight -= raceHeight;

        DrawSide(g, isHome: true,
            column: new Rectangle(pad, pad, columnWidth - pad * 2, nameHeight),
            number: new Rectangle(pad, numberTop, columnWidth - pad * 2, numberHeight),
            race: new Rectangle(pad, numberTop + numberHeight, columnWidth - pad * 2, raceHeight),
            buttons: new Rectangle(pad, buttonTop, columnWidth - pad * 2, buttonHeight));

        DrawSide(g, isHome: false,
            column: new Rectangle(w - columnWidth + pad, pad, columnWidth - pad * 2, nameHeight),
            number: new Rectangle(w - columnWidth + pad, numberTop, columnWidth - pad * 2, numberHeight),
            race: new Rectangle(w - columnWidth + pad, numberTop + numberHeight, columnWidth - pad * 2, raceHeight),
            buttons: new Rectangle(w - columnWidth + pad, buttonTop, columnWidth - pad * 2, buttonHeight));

        // Two narrow bars down the middle, running the whole height.
        var barGap = Math.Max(4, (int)(w * 0.014));
        var barWidth = (barArea - barGap) / 2;
        var barTop = pad;
        var barHeight = h - pad * 2;

        DrawBar(g, new Rectangle(columnWidth, barTop, barWidth, barHeight),
                _state.HomeScore, _state.HomeTarget, _state.HomeWon, _state.HomeOnHill);

        DrawBar(g, new Rectangle(columnWidth + barWidth + barGap, barTop, barWidth, barHeight),
                _state.AwayScore, _state.AwayTarget, _state.AwayWon, _state.AwayOnHill);

        // Drawn last so it sits over the bars it has to overlap.
        DrawAction(g, w, h);
    }

    private void DrawSide(Graphics g, bool isHome, Rectangle column, Rectangle number, Rectangle race, Rectangle buttons)
    {
        var names = isHome ? _state.HomeNames : _state.AwayNames;
        var breaking = _state.Breaking == (isHome ? Side.Home : Side.Away);
        var score = isHome ? _state.HomeScore : _state.AwayScore;
        var target = isHome ? _state.HomeTarget : _state.AwayTarget;
        var won = isHome ? _state.HomeWon : _state.AwayWon;
        var onHill = isHome ? _state.HomeOnHill : _state.AwayOnHill;

        DrawNames(g, column, names, breaking, isHome);
        DrawBigNumber(g, number, score, Shade(won, onHill));
        DrawRace(g, race, target);

        var gap = (int)(buttons.Width * 0.05);
        var buttonWidth = (buttons.Width - gap) / 2;
        var minus = new Rectangle(buttons.Left, buttons.Top, buttonWidth, buttons.Height);
        var plus = new Rectangle(buttons.Left + buttonWidth + gap, buttons.Top, buttonWidth, buttons.Height);

        if (isHome)
        {
            _homeMinus = minus;
            _homePlus = plus;
        }
        else
        {
            _awayMinus = minus;
            _awayPlus = plus;
        }

        var key = isHome ? "A" : "B";
        DrawButton(g, minus, "−", $"ALT+{key}");
        DrawButton(g, plus, "+", key);
    }

    private static void DrawNames(Graphics g, Rectangle area, string[] names, bool breaking, bool isHome)
    {
        if (names.Length == 0) return;

        // A rack marker's worth of room is kept on the outer edge whether or
        // not the dot is showing, so names do not shift when the break moves.
        var dot = (int)Math.Min(area.Width * 0.06f, area.Height * 0.30f);
        var reserved = dot + dot / 2;

        var text = new RectangleF(
            isHome ? area.Left + reserved : area.Left,
            area.Top,
            area.Width - reserved,
            area.Height);

        using var block = NameLayout.Fit(g, names, separator: null,
            maxSize: area.Height * 0.46f, minSize: 11f,
            box: text.Size, style: FontStyle.Bold);

        NameLayout.Draw(g, block, text, isHome ? NameAlign.Left : NameAlign.Right, Paper, NameVAlign.Top);

        if (!breaking) return;

        using var gold = new SolidBrush(Won);
        var firstLine = area.Top + block.LineHeight / 2f;
        var x = isHome ? area.Left : area.Right - dot;
        g.FillEllipse(gold, x, firstLine - dot / 2f, dot, dot);
    }

    /// <summary>
    /// The score, filling its half and centred in it. Built as an outline and
    /// filled rather than drawn as text: at this size a glyph rasterised to a
    /// fixed font size shows its pixel grid, whereas a scaled path stays smooth
    /// however big it gets.
    /// </summary>
    private static void DrawBigNumber(Graphics g, Rectangle area, int score, Color colour)
    {
        if (area.Height < 20 || area.Width < 20) return;

        using var path = new GraphicsPath();
        path.AddString(score.ToString(), FontFamily.GenericSansSerif, (int)FontStyle.Bold,
            100f, new PointF(0, 0), StringFormat.GenericTypographic);

        var bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var scale = Math.Min(area.Width / bounds.Width, area.Height / bounds.Height);

        using var matrix = new Matrix();
        matrix.Translate(
            area.Left + (area.Width - bounds.Width * scale) / 2f,
            area.Top + (area.Height - bounds.Height * scale) / 2f);
        matrix.Scale(scale, scale);
        matrix.Translate(-bounds.Left, -bounds.Top);
        path.Transform(matrix);

        using var brush = new SolidBrush(colour);
        g.FillPath(brush, path);
    }

    /// <summary>
    /// The race, under the score. Small and muted on purpose: the bar already
    /// says how far along a side is, this says what it is chasing. It is drawn
    /// per side rather than once, because on a handicap the two sides are
    /// chasing different numbers and one figure could only be right for both
    /// by luck.
    /// </summary>
    private static void DrawRace(Graphics g, Rectangle area, int target)
    {
        if (target < 1 || area.Height < 10 || area.Width < 40) return;

        var text = $"RACE TO {target}";
        using var font = new Font(FontFamily.GenericSansSerif, area.Height * 0.74f, FontStyle.Bold, GraphicsUnit.Pixel);

        var size = g.MeasureString(text, font);
        using var brush = new SolidBrush(Hint);
        g.DrawString(text, font, brush,
            area.Left + (area.Width - size.Width) / 2f,
            area.Top + (area.Height - size.Height) / 2f);
    }

    /// <summary>
    /// The one button that matters when a set is over, put where the eye
    /// already is - between the two scores.
    /// </summary>
    private void DrawAction(Graphics g, int w, int h)
    {
        if (!_state.Decided)
        {
            _action = Rectangle.Empty;
            return;
        }

        var width = Math.Min((int)(w * 0.30f), 300);
        var height = Math.Clamp((int)(h * 0.16f), 46, 74);
        _action = new Rectangle((w - width) / 2, (h - height) / 2, width, height);

        // A gap punched out of the background first: by the time this shows,
        // the winning bar behind it is gold too, and without the gap the two
        // merge into one shape.
        var halo = Rectangle.Inflate(_action, (int)(height * 0.16f), (int)(height * 0.16f));
        using (var gap = RoundedRect(halo, height * 0.30f))
        using (var background = new SolidBrush(Ink))
        {
            g.FillPath(background, gap);
        }

        using var path = RoundedRect(_action, height * 0.22f);
        using var face = new SolidBrush(_hot == _action ? ActionHover : Won);
        g.FillPath(face, path);

        using var font = new Font(FontFamily.GenericSansSerif, height * 0.36f, FontStyle.Bold, GraphicsUnit.Pixel);
        var size = g.MeasureString(_actionText, font);
        using var ink = new SolidBrush(Ink);
        g.DrawString(_actionText, font, ink,
            _action.Left + (_action.Width - size.Width) / 2f,
            _action.Top + (_action.Height - size.Height) / 2f);
    }

    /// <summary>
    /// One box per rack in this side's race, filling from the bottom up.
    /// </summary>
    private static void DrawBar(Graphics g, Rectangle area, int score, int target, bool won, bool onHill)
    {
        if (target < 1) return;

        var gap = Math.Max(3, area.Height / (target * 14));
        var boxHeight = (area.Height - gap * (target - 1)) / (float)target;
        var radius = Math.Min(boxHeight, area.Width) * 0.22f;
        var filled = Shade(won, onHill);

        for (var i = 0; i < target; i++)
        {
            var top = area.Bottom - (i + 1) * boxHeight - i * gap;
            var box = new RectangleF(area.Left, top, area.Width, boxHeight);

            using var path = RoundedRect(box, radius);
            using var brush = new SolidBrush(i < score ? filled : Unwon);
            g.FillPath(brush, path);
        }
    }

    private void DrawButton(Graphics g, Rectangle area, string glyph, string shortcut)
    {
        var hot = _hot == area;
        using var path = RoundedRect(area, Math.Min(area.Height, area.Width) * 0.18f);
        using var face = new SolidBrush(hot ? ButtonHover : ButtonFace);
        g.FillPath(face, path);

        using var glyphFont = new Font(FontFamily.GenericSansSerif, area.Height * 0.46f, FontStyle.Bold, GraphicsUnit.Pixel);
        var glyphSize = g.MeasureString(glyph, glyphFont);
        using var ink = new SolidBrush(Paper);
        g.DrawString(glyph, glyphFont, ink,
            area.Left + (area.Width - glyphSize.Width) / 2f,
            area.Top + area.Height * 0.10f);

        // The keyboard shortcut, small, under the glyph.
        using var hintFont = new Font(FontFamily.GenericSansSerif, area.Height * 0.19f, FontStyle.Bold, GraphicsUnit.Pixel);
        var hintSize = g.MeasureString(shortcut, hintFont);
        using var muted = new SolidBrush(Hint);
        g.DrawString(shortcut, hintFont, muted,
            area.Left + (area.Width - hintSize.Width) / 2f,
            area.Bottom - hintSize.Height - area.Height * 0.06f);
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var was = _hot;
        _hot = Hit(e.Location);
        if (_hot != was) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot is null) return;
        _hot = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        // Checked first: it overlaps the bars and nothing else is under it.
        if (!_action.IsEmpty && _action.Contains(e.Location)) ActionClicked?.Invoke(this, EventArgs.Empty);
        else if (_homeMinus.Contains(e.Location)) HomeDelta?.Invoke(this, -1);
        else if (_homePlus.Contains(e.Location)) HomeDelta?.Invoke(this, +1);
        else if (_awayMinus.Contains(e.Location)) AwayDelta?.Invoke(this, -1);
        else if (_awayPlus.Contains(e.Location)) AwayDelta?.Invoke(this, +1);
    }

    private Rectangle? Hit(Point p)
    {
        if (!_action.IsEmpty && _action.Contains(p)) return _action;
        if (_homeMinus.Contains(p)) return _homeMinus;
        if (_homePlus.Contains(p)) return _homePlus;
        if (_awayMinus.Contains(p)) return _awayMinus;
        if (_awayPlus.Contains(p)) return _awayPlus;
        return null;
    }
}
