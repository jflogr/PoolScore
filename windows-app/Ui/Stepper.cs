using System.ComponentModel;

namespace PoolScoreTracker.Ui;

/// <summary>
/// A number with a big button either side of it. The spinner arrows on a
/// NumericUpDown are a few pixels tall, which is no use on a laptop
/// touchscreen at the side of a pool table.
/// </summary>
public sealed class Stepper : UserControl
{
    private readonly Button _down = Step("−");
    private readonly Button _up = Step("+");
    private readonly Label _display = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold),
        ForeColor = ColorTranslator.FromHtml("#2b3846")
    };

    private int _value = 3;

    public Stepper()
    {
        Height = 44;
        Width = 150;
        Margin = new Padding(0, 0, 12, 8);

        _up.Dock = DockStyle.Right;
        _down.Click += (_, _) => Nudge(-1);
        _up.Click += (_, _) => Nudge(+1);

        // Fill goes in first so the buttons dock outside it.
        Controls.Add(_display);
        Controls.Add(_down);
        Controls.Add(_up);

        UpdateDisplay();
    }

    [DefaultValue(1)]
    public int Minimum { get; init; } = 1;

    [DefaultValue(9)]
    public int Maximum { get; init; } = 9;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            if (clamped == _value) return;

            _value = clamped;
            UpdateDisplay();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Square buttons, so they stay easy to hit whatever height is set.
        _down.Width = Height;
        _up.Width = Height;
    }

    private void Nudge(int delta) => Value = _value + delta;

    private void UpdateDisplay()
    {
        _display.Text = _value.ToString();
        _down.Enabled = _value > Minimum;
        _up.Enabled = _value < Maximum;
    }

    private static Button Step(string glyph) => new()
    {
        Text = glyph,
        Dock = DockStyle.Left,
        Width = 44,
        FlatStyle = FlatStyle.System,
        Font = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold),
        TabStop = false
    };
}
