using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Ui;

/// <summary>
/// The roster, out of the way until you need it. Edits apply straight to the
/// session, so there is nothing to save or cancel - closing it is the only
/// thing left to do.
/// </summary>
public sealed class PlayersDialog : Form
{
    private const int TouchHeight = 46;

    private readonly Session _session;
    private readonly ListView _roster = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
        Font = new Font(FontFamily.GenericSansSerif, 11.5f)
    };

    public PlayersDialog(Session session)
    {
        _session = session;

        Text = "Players";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 460);
        MinimumSize = new Size(420, 360);
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = ColorTranslator.FromHtml("#f5f7fa");
        Padding = new Padding(12);

        _roster.Columns.Add("Player", 260);
        _roster.Columns.Add("Shown as", 170);
        _roster.DoubleClick += (_, _) => Rename();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = TouchHeight + 14,
            Padding = new Padding(0, 8, 0, 0)
        };

        buttons.Controls.Add(Button("Add", Add));
        buttons.Controls.Add(Button("Rename", Rename));
        buttons.Controls.Add(Button("Remove", Remove));

        var close = Button("Close", Close);
        close.Margin = new Padding(28, 0, 0, 0);
        buttons.Controls.Add(close);

        Controls.Add(_roster);
        Controls.Add(buttons);

        _session.Changed += OnSessionChanged;
        Fill();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _session.Changed -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Fill();

    private static Button Button(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(88, TouchHeight),
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.System,
            Font = new Font(FontFamily.GenericSansSerif, 11f)
        };

        button.Click += (_, _) => onClick();
        return button;
    }

    private void Fill()
    {
        if (IsDisposed) return;

        _roster.BeginUpdate();
        _roster.Items.Clear();

        foreach (var player in _session.Roster)
        {
            // What the scoreboard will actually show for them, shortened.
            var slot = _session.Current.Lineup.IndexOf(player.Id);
            var shown = slot >= 0 ? ShownAs(slot) : "not playing";

            _roster.Items.Add(new ListViewItem([player.Name, shown]) { Tag = player });
        }

        _roster.EndUpdate();
    }

    private string ShownAs(int slot)
    {
        if (_session.IsRoundRobin) return _session.NameFor($"p{slot + 1}");

        // In a match the slots map straight onto the two sides.
        var view = _session.View();
        var size = _session.Config.TeamSize;

        return slot < size
            ? view.HomeNames.ElementAtOrDefault(slot) ?? "-"
            : view.AwayNames.ElementAtOrDefault(slot - size) ?? "-";
    }

    private RosterPlayer? Selected() =>
        _roster.SelectedItems.Count == 0 ? null : _roster.SelectedItems[0].Tag as RosterPlayer;

    private void Add()
    {
        var name = Prompt("Add player", string.Empty);
        if (name is not null) _session.AddPlayer(name);
    }

    private void Rename()
    {
        if (Selected() is not { } player) return;

        var name = Prompt("Rename player", player.Name);
        if (name is not null) _session.RenamePlayer(player.Id, name);
    }

    private void Remove()
    {
        if (Selected() is not { } player) return;

        var answer = MessageBox.Show(this, $"Remove {player.Name} from the list?", "Players",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer == DialogResult.Yes) _session.RemovePlayer(player.Id);
    }

    /// <summary>A one-line text prompt; null if the user backed out.</summary>
    private string? Prompt(string title, string initial)
    {
        using var dialog = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(380, 140),
            MinimizeBox = false,
            MaximizeBox = false
        };

        var input = new TextBox
        {
            Text = initial,
            Location = new Point(16, 22),
            Width = 348,
            Font = new Font(FontFamily.GenericSansSerif, 13f)
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(184, 78),
            Size = new Size(88, 40)
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(280, 78),
            Size = new Size(88, 40)
        };

        dialog.Controls.AddRange([input, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK) return null;

        var value = input.Text.Trim();
        return value.Length == 0 ? null : value;
    }
}
