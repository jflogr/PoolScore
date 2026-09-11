using PoolScoreTracker.Domain;

namespace PoolScoreTracker.Ui;

/// <summary>
/// The roster, out of the way until you need it. Edits apply straight to the
/// session, so there is nothing to save or cancel and the window's own close
/// button is the only way out it needs.
/// </summary>
public sealed class PlayersDialog : Form
{
    private static readonly Color Ink = ColorTranslator.FromHtml("#2b3846");
    private static readonly Color Danger = ColorTranslator.FromHtml("#b23b3b");

    private readonly Session _session;
    private readonly ToolStrip _commands = new();
    private readonly ListView _roster = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
        OwnerDraw = true,
        Font = new Font(FontFamily.GenericSansSerif, 11.5f)
    };

    private ToolStripButton _edit = null!;
    private Bitmap _bin = null!;
    private Bitmap _binSelected = null!;
    private int _binColumnWidth;

    public PlayersDialog(Session session)
    {
        _session = session;

        Text = "Players";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 460);
        MinimumSize = new Size(400, 340);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Icon = Icons.App;
        BackColor = ColorTranslator.FromHtml("#f5f7fa");

        BuildCommands();

        _binColumnWidth = _commands.ImageScalingSize.Width * 2;
        _roster.Columns.Add("Player", 300);
        _roster.Columns.Add(string.Empty, _binColumnWidth);

        _roster.DrawColumnHeader += DrawHeader;
        _roster.DrawItem += DrawRow;
        _roster.MouseClick += OnRosterClick;
        _roster.DoubleClick += (_, _) => Rename();
        _roster.SelectedIndexChanged += (_, _) => SyncCommands();
        _roster.Resize += (_, _) => FitColumns();

        Controls.Add(_roster);
        Controls.Add(_commands);

        _session.Changed += OnSessionChanged;
        Fill();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitColumns();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _session.Changed -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Fill();

    private void BuildCommands()
    {
        _commands.Dock = DockStyle.Top;
        _commands.GripStyle = ToolStripGripStyle.Hidden;
        _commands.BackColor = ColorTranslator.FromHtml("#f5f7fa");
        _commands.Padding = new Padding(6, 6, 6, 6);
        _commands.Font = new Font(FontFamily.GenericSansSerif, 10.5f);
        _commands.Renderer = new ToolStripProfessionalRenderer { RoundedEdges = false };

        var size = (int)Math.Round(24 * DeviceDpi / 96.0);
        _commands.ImageScalingSize = new Size(size, size);
        _bin = Icons.Render(Icons.Delete, size, Danger);

        // Red on the selection highlight all but disappears.
        _binSelected = Icons.Render(Icons.Delete, size, Color.White);

        _commands.Items.Add(Command("Add player", Icons.Add, size, Add));
        _edit = Command("Rename", Icons.Edit, size, Rename);
        _commands.Items.Add(_edit);
    }

    private static ToolStripButton Command(string text, string iconPath, int size, Action onClick)
    {
        var button = new ToolStripButton(text, Icons.Render(iconPath, size, Ink))
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(2, 0, 2, 0),
            AutoSize = true
        };

        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>The name column takes whatever the bin column does not.</summary>
    private void FitColumns()
    {
        if (_roster.Columns.Count < 2) return;

        var available = _roster.ClientSize.Width - _binColumnWidth;
        if (available > 80) _roster.Columns[0].Width = available;
    }

    // --------------------------------------------------------------- drawing

    private static void DrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e) => e.DrawDefault = true;

    /// <summary>
    /// Drawn by hand so each row can carry its own bin. A ListView has no way
    /// to put a control in a cell, and a delete that lives on the row it
    /// deletes is worth the drawing code.
    /// </summary>
    private void DrawRow(object? sender, DrawListViewItemEventArgs e)
    {
        var selected = e.Item.Selected;
        var background = selected ? SystemColors.Highlight : _roster.BackColor;

        using (var brush = new SolidBrush(background))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        var name = e.Item.Text;
        var nameArea = new Rectangle(
            e.Bounds.Left + 6, e.Bounds.Top,
            Math.Max(0, e.Bounds.Width - _binColumnWidth - 12), e.Bounds.Height);

        TextRenderer.DrawText(e.Graphics, name, _roster.Font, nameArea,
            selected ? SystemColors.HighlightText : Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.Graphics.DrawImage(selected ? _binSelected : _bin, BinBounds(e.Bounds));
    }

    private Rectangle BinBounds(Rectangle row)
    {
        var size = _bin.Width;
        return new Rectangle(
            row.Right - _binColumnWidth + (_binColumnWidth - size) / 2,
            row.Top + (row.Height - size) / 2,
            size, size);
    }

    private void OnRosterClick(object? sender, MouseEventArgs e)
    {
        var hit = _roster.HitTest(e.Location);
        if (hit.Item is null) return;

        if (BinBounds(hit.Item.Bounds).Contains(e.Location)) Remove(hit.Item.Tag as RosterPlayer);
    }

    // ---------------------------------------------------------------- edits

    private void Fill()
    {
        if (IsDisposed) return;

        _roster.BeginUpdate();
        _roster.Items.Clear();

        foreach (var player in _session.Roster)
        {
            _roster.Items.Add(new ListViewItem(player.Name) { Tag = player });
        }

        _roster.EndUpdate();
        SyncCommands();
    }

    private void SyncCommands() => _edit.Enabled = Selected() is not null;

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

    private void Remove(RosterPlayer? player)
    {
        if (player is null) return;

        var answer = MessageBox.Show(this,
            $"Remove {player.Name} from the list?", "Players",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

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
            MaximizeBox = false,
            Icon = Icon,
            ShowInTaskbar = false
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
