using PoolScoreTracker.Domain;
using PoolScoreTracker.Server;
using QRCoder;

namespace PoolScoreTracker.Ui;

/// <summary>
/// The operator's window. The top half is the scoring area, sized to be read
/// from across the room; everything below it is reference material that only
/// has to be readable at arm's length, and controls that only have to be big
/// enough to tap on a laptop touchscreen.
/// </summary>
public sealed class MainWindow : Form
{
    /// <summary>Comfortable to tap, no bigger - this is not scoreboard furniture.</summary>
    private const int TouchHeight = 46;

    /// <summary>
    /// As narrow as a phone camera will still read at arm's length. The address
    /// under it is the fallback if it will not scan.
    /// </summary>
    private const int QrSize = 116;

    private static readonly Color Ink = ColorTranslator.FromHtml("#2b3846");
    private static readonly Color Paper = ColorTranslator.FromHtml("#f5f7fa");

    /// <summary>An entry in a line-up picker; a null id means the slot is empty.</summary>
    private sealed record SlotChoice(string? Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly Session _session;
    private readonly ScoringPanel _scoring = new();
    private readonly ToolStrip _commands = new();
    private readonly TabControl _tabs = new();
    private readonly ListView _leaders = NewList();
    private readonly ListView _matches = NewList();

    private ToolStripButton _undo = null!;

    private PlayersDialog? _players;

    private readonly ComboBox _modeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Font = new Font(FontFamily.GenericSansSerif, 11f) };
    private readonly ComboBox _breakBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Font = new Font(FontFamily.GenericSansSerif, 11f) };
    private readonly ComboBox _nameBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Font = new Font(FontFamily.GenericSansSerif, 11f) };
    private readonly Stepper _raceHome = new();
    private readonly Stepper _raceAway = new();
    private readonly Label _raceHomeLabel = new() { AutoSize = true, Padding = new Padding(0, 8, 0, 0), Font = new Font(FontFamily.GenericSansSerif, 10.5f) };
    private readonly Label _raceAwayLabel = new() { AutoSize = true, Padding = new Padding(0, 8, 0, 0), Font = new Font(FontFamily.GenericSansSerif, 10.5f) };
    private readonly FlowLayoutPanel _lineup = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };

    private readonly PictureBox _qrView = new()
    {
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.White,
        Visible = false
    };

    private readonly Label _phoneLabel = new()
    {
        ForeColor = ColorTranslator.FromHtml("#5b6b7d"),
        Font = new Font(FontFamily.GenericSansSerif, 8f)
    };

    // Scoring a rack fires several changes in a row; saving on a short delay
    // writes once when the flurry stops rather than once per keystroke.
    private readonly System.Windows.Forms.Timer _save = new() { Interval = 1200 };

    private readonly ScoreServer _server = new();
    private readonly List<ComboBox> _slots = [];

    private TabPage _settingsPage = null!;
    private TabPage _matchesPage = null!;
    private TabPage _leaderboardPage = null!;

    private string _lineupSignature = string.Empty;
    private bool _loading;

    private BannerForm? _banner;

    public MainWindow(Session session)
    {
        _session = session;

        Text = "Pool Score";
        ClientSize = new Size(960, 1000);
        MinimumSize = new Size(620, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Paper;

        BuildCommands();
        BuildTabs();

        _scoring.Dock = DockStyle.Top;
        _scoring.Height = ClientSize.Height / 2;
        _scoring.HomeDelta += (_, delta) => _session.Adjust(Side.Home, delta);
        _scoring.AwayDelta += (_, delta) => _session.Adjust(Side.Away, delta);
        _scoring.ActionClicked += (_, _) => FinishSet();

        // Docking resolves from the last control added outwards, so this reads
        // backwards: toolbar at the very top, then the scoreboard full width,
        // then the phone strip down the right of what is left, and the tabs
        // filling the rest.
        Controls.Add(_tabs);
        Controls.Add(BuildPhoneStrip());
        Controls.Add(_scoring);
        Controls.Add(_commands);

        _save.Tick += (_, _) =>
        {
            _save.Stop();
            SessionStore.Save(_session);
        };

        _session.Changed += (_, _) =>
        {
            Sync();

            // Snapshots are built here, on the UI thread, so the server never
            // reaches into the live session from a request thread.
            if (_server.Running) _server.Publish(Snapshot.From(_session, 0));

            _save.Stop();
            _save.Start();
        };

        Sync();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            // Phone sharing is a bonus; if the port is taken the app carries on.
            var started = await _server.StartAsync();

            // The window can be gone by the time the server finishes starting,
            // and an async void that touches a disposed form takes the app
            // down with it.
            if (IsDisposed || Disposing) return;

            if (!started)
            {
                _phoneLabel.Text = "Phone view unavailable - port 4174 is in use.";
                return;
            }

            _server.Publish(Snapshot.From(_session, 0));

            var address = _server.Addresses.FirstOrDefault();
            if (address is null)
            {
                _phoneLabel.Text = "Phone view running, but this PC is not on a network.";
                return;
            }

            // Shown without the scheme so it fits the narrow strip on one line
            // instead of wrapping mid-port. The code itself carries the full URL.
            _phoneLabel.Text = address.Replace("http://", string.Empty).TrimEnd('/');
            _qrView.Image = MakeQr(address);
            _qrView.Visible = _qrView.Image is not null;
        }
        catch (Exception)
        {
            if (!IsDisposed) _phoneLabel.Text = "Phone view could not start.";
        }
    }

    /// <summary>Scan-to-watch code for the phone view, or null if it cannot be made.</summary>
    private static Image? MakeQr(string url)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(8);

            using var stream = new MemoryStream(png);
            using var loaded = new Bitmap(stream);

            // Copied off the stream, which a Bitmap would otherwise keep open.
            return new Bitmap(loaded);
        }
        catch
        {
            return null; // the address line still tells them where to go
        }
    }

    /// <summary>The scoring area takes the top half, whatever the window size.</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _scoring.Height = ClientSize.Height / 2;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        // Whatever the debounce was still holding, write it now.
        _save.Stop();
        SessionStore.Save(_session);

        _banner?.Close();

        // Not awaited: the process is on its way out either way, and blocking
        // the UI thread on a shutdown handshake would only risk a deadlock.
        _ = _server.StopAsync();
    }

    /// <summary>
    /// A and B add a rack to that side; hold Alt to take one off. Handled here
    /// rather than on the panel so they work wherever the focus happens to be -
    /// except in a dropdown, where a letter belongs to the list.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        var alt = (keyData & Keys.Alt) == Keys.Alt;
        var otherModifier = (keyData & (Keys.Control | Keys.Shift)) != 0;

        if (!otherModifier && key is Keys.A or Keys.B && ActiveControl is not (ComboBox or TextBox))
        {
            _session.Adjust(key == Keys.A ? Side.Home : Side.Away, alt ? -1 : +1);
            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    // ------------------------------------------------------------ building

    private void BuildCommands()
    {
        _commands.Dock = DockStyle.Top;
        _commands.GripStyle = ToolStripGripStyle.Hidden;
        _commands.BackColor = Paper;
        _commands.Padding = new Padding(8, 6, 8, 6);
        _commands.Font = new Font(FontFamily.GenericSansSerif, 10.5f);
        _commands.Renderer = new ToolStripProfessionalRenderer { RoundedEdges = false };

        // Rendered at whatever this display actually needs, so the outlines stay
        // sharp on a scaled screen instead of being a 24px bitmap stretched up.
        var icon = (int)Math.Round(24 * DeviceDpi / 96.0);
        _commands.ImageScalingSize = new Size(icon, icon);

        _undo = Command("Previous match", Icons.Undo, icon, _session.UndoLastCommit);

        _commands.Items.Add(Command("Players", Icons.Group, icon, ShowPlayers));
        _commands.Items.Add(Command("Swap break", Icons.SwapHoriz, icon, _session.SwapBreak));
        _commands.Items.Add(Command("Reset", Icons.RestartAlt, icon, ResetScores));
        _commands.Items.Add(_undo);

        // Off on its own at the far end: it opens a window rather than
        // changing the score, so it does not belong with the rest.
        var banner = Command("Banner", Icons.Tv, icon, ToggleBanner);
        banner.Alignment = ToolStripItemAlignment.Right;
        _commands.Items.Add(banner);
    }

    /// <summary>
    /// The scan-to-watch code, down the right-hand side where it is always in
    /// view. It lived on the settings tab, which meant hunting for it whenever
    /// somebody new wanted to follow the game.
    ///
    /// Docked after the scoreboard on purpose, so it takes its width from the
    /// tabs below rather than narrowing the scoreboard - that is the part that
    /// has to be read from across the room.
    /// </summary>
    private Control BuildPhoneStrip()
    {
        var strip = new Panel
        {
            Dock = DockStyle.Right,
            Width = QrSize + 12,
            BackColor = Paper,
            Padding = new Padding(6, 10, 6, 8)
        };

        // Stays hidden until the server is up and there is a code to show,
        // rather than flashing an empty white square on startup.
        _qrView.Dock = DockStyle.Top;
        _qrView.Height = QrSize;

        _phoneLabel.Dock = DockStyle.Top;
        _phoneLabel.AutoSize = true;
        _phoneLabel.MaximumSize = new Size(QrSize + 4, 0);
        _phoneLabel.Padding = new Padding(0, 6, 0, 0);

        var heading = Heading("Scan to watch");
        heading.Dock = DockStyle.Top;
        heading.Margin = Padding.Empty;

        // Added bottom-up: heading, then code, then the address under it.
        strip.Controls.Add(_phoneLabel);
        strip.Controls.Add(_qrView);
        strip.Controls.Add(heading);
        return strip;
    }

    private static ToolStripButton Command(string text, string iconPath, int iconSize, Action onClick) =>
        Command(text, Icons.Render(iconPath, iconSize, Ink), onClick);

    private static ToolStripButton Command(string text, Image icon, Action onClick)
    {
        var button = new ToolStripButton(text, icon)
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(2, 0, 2, 0),
            AutoSize = true
        };

        button.Click += (_, _) => onClick();
        return button;
    }

    private void BuildTabs()
    {
        _tabs.Dock = DockStyle.Fill;

        // Tabs only have to be tappable, so the strip gets a touch-sized row
        // rather than the default sliver.
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(150, 42);
        _tabs.Padding = new Point(12, 8);
        _tabs.Font = new Font(FontFamily.GenericSansSerif, 11f);

        _settingsPage = Page("Match settings", BuildSettingsTab());
        _matchesPage = Page("Matches", _matches);
        _leaderboardPage = Page("Leaderboard", _leaders);

        // The last two only mean anything in a round robin; SyncTabs takes
        // them away for a one-off match rather than showing empty tables.
        _tabs.TabPages.Add(_settingsPage);
        _tabs.TabPages.Add(_matchesPage);
        _tabs.TabPages.Add(_leaderboardPage);

        _leaders.Columns.Add("Player", 250);
        _leaders.Columns.Add("Wins", 100);
        _leaders.Columns.Add("Possible", 110);

        _matches.Columns.Add("Set", 55);
        _matches.Columns.Add("Round", 70);
        _matches.Columns.Add("Home", 230);
        _matches.Columns.Add("Score", 80);
        _matches.Columns.Add("Away", 230);
        _matches.Columns.Add("Sitting", 120);
        _matches.Columns.Add("Status", 100);
    }

    /// <summary>Format, race, house rules, who plays where, and the phone code.</summary>
    private Control BuildSettingsTab()
    {
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        _modeBox.DisplayMember = nameof(ModeConfig.Label);
        foreach (var config in Modes.All.Values) _modeBox.Items.Add(config);
        _modeBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading && _modeBox.SelectedItem is ModeConfig config) _session.SetMode(config.Key);
        };

        _breakBox.Items.AddRange(["Alternate break", "Winner breaks"]);
        _breakBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading) _session.SetBreakRule(_breakBox.SelectedIndex == 1 ? BreakRule.Winner : BreakRule.Alternate);
        };

        _nameBox.Items.AddRange(["First name", "Full name"]);
        _nameBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading) _session.SetNameStyle(_nameBox.SelectedIndex == 1 ? NameStyle.Full : NameStyle.First);
        };

        _raceHome.ValueChanged += (_, _) => { if (!_loading) _session.SetTarget(Side.Home, _raceHome.Value); };
        _raceAway.ValueChanged += (_, _) => { if (!_loading) _session.SetTarget(Side.Away, _raceAway.Value); };

        left.Controls.Add(Heading("Format"));
        left.Controls.Add(_modeBox);
        left.Controls.Add(_raceHomeLabel);
        left.Controls.Add(_raceHome);
        left.Controls.Add(_raceAwayLabel);
        left.Controls.Add(_raceAway);
        left.Controls.Add(Heading("Break"));
        left.Controls.Add(_breakBox);
        left.Controls.Add(Heading("Names"));
        left.Controls.Add(_nameBox);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 0, 0, 0)
        };

        right.Controls.Add(Heading("Line-up"));
        right.Controls.Add(_lineup);

        split.Controls.Add(left, 0, 0);
        split.Controls.Add(right, 1, 0);
        return split;
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(FontFamily.GenericSansSerif, 11.5f, FontStyle.Bold),
        ForeColor = Ink,
        Margin = new Padding(0, 10, 0, 4)
    };

    private static TabPage Page(string text, Control content)
    {
        var page = new TabPage(text) { Padding = new Padding(6), BackColor = Color.White };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private static ListView NewList() => new()
    {
        View = View.Details,
        FullRowSelect = true,
        GridLines = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
        Font = new Font(FontFamily.GenericSansSerif, 11.5f)
    };

    // -------------------------------------------------------- roster edits

    /// <summary>
    /// Clears this format's scores. Destructive and one tap away from the
    /// scoring buttons, so it always asks first.
    /// </summary>
    private void ResetScores()
    {
        var answer = MessageBox.Show(this,
            $"Reset all {_session.Config.Label} scores?\n\nPlayers and the line-up are kept.",
            "Pool Score", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer == DialogResult.Yes) _session.ResetScores();
    }

    // ------------------------------------------------------------- refresh

    private void Sync()
    {
        _loading = true;
        try
        {
            var session = _session;
            _scoring.State = BuildScoringState(session);
            if (_banner is not null) _banner.State = BuildBannerState(session);

            SyncCommands(session);
            SyncTabs(session);
            SyncSetup(session);
            FillLeaders(session);
            FillMatches(session);
        }
        finally
        {
            _loading = false;
        }
    }

    private void SyncCommands(Session session)
    {
        var roundRobin = session.IsRoundRobin;

        // Going back a match only makes sense in a round robin, once one has
        // been played, and while the current one is untouched - otherwise it
        // would throw away a score in progress.
        var current = session.CurrentSet();
        _undo.Visible = roundRobin
            && session.RoundRobin.Scores.Any(score => score.Committed)
            && (current is not { } playing || (playing.Score.Home == 0 && playing.Score.Away == 0));

        // The centre button only appears once a side has won, so it can say
        // what actually happens next rather than "commit".
        _scoring.ActionText = roundRobin ? "Next match" : "New match";
    }

    /// <summary>
    /// The centre button: in a round robin that records the set and moves on,
    /// in a one-off match it just clears the board for another race.
    /// </summary>
    private void FinishSet()
    {
        if (!_session.IsRoundRobin)
        {
            _session.StartNewMatch();
            return;
        }

        if (!_session.TryCommit(out var problem) && problem is not null)
        {
            MessageBox.Show(this, problem, "Pool Score",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>The roster, in its own window rather than a tab nobody needs open.</summary>
    private void ShowPlayers()
    {
        if (_players is not null && !_players.IsDisposed)
        {
            _players.BringToFront();
            _players.Focus();
            return;
        }

        _players = new PlayersDialog(_session) { Owner = this };
        _players.FormClosed += (_, _) => _players = null;
        _players.Show();
    }

    /// <summary>
    /// A schedule and a table of wins only exist in a round robin, so those two
    /// tabs come and go with the format rather than sitting there empty.
    /// </summary>
    private void SyncTabs(Session session)
    {
        var wanted = session.IsRoundRobin;
        if (wanted == _tabs.TabPages.Contains(_matchesPage)) return;

        if (wanted)
        {
            _tabs.TabPages.Add(_matchesPage);
            _tabs.TabPages.Add(_leaderboardPage);
        }
        else
        {
            _tabs.TabPages.Remove(_matchesPage);
            _tabs.TabPages.Remove(_leaderboardPage);
        }
    }

    private void SyncSetup(Session session)
    {
        _modeBox.SelectedItem = session.Config;
        _breakBox.SelectedIndex = session.BreakRule == BreakRule.Winner ? 1 : 0;
        _nameBox.SelectedIndex = session.NameStyle == NameStyle.Full ? 1 : 0;

        // A round-robin races to one number; a match gives each side its own,
        // which is the whole mechanism behind a handicap.
        var split = !session.IsRoundRobin;
        _raceHomeLabel.Text = split ? "Home races to" : "Race to";
        _raceHome.Value = session.TargetFor(Side.Home);
        _raceAwayLabel.Visible = split;
        _raceAway.Visible = split;
        if (split) _raceAway.Value = session.TargetFor(Side.Away);
        _raceAwayLabel.Text = "Away races to";

        SyncLineup(session);
    }

    private void SyncLineup(Session session)
    {
        // Only rebuild the pickers when the shape changes; otherwise just move
        // the selections, so a dropdown does not vanish under the user.
        var signature = session.ModeKey + "|" + string.Join(",", session.Roster.Select(p => p.Id + ":" + p.Name));

        if (signature != _lineupSignature)
        {
            _lineupSignature = signature;
            RebuildLineup(session);
        }

        for (var slot = 0; slot < _slots.Count; slot++)
        {
            var id = session.Current.Lineup.ElementAtOrDefault(slot);
            _slots[slot].SelectedItem = _slots[slot].Items
                .Cast<SlotChoice>()
                .FirstOrDefault(choice => choice.Id == id);
        }
    }

    private void RebuildLineup(Session session)
    {
        _lineup.Controls.Clear();
        _slots.Clear();

        var choices = new List<SlotChoice> { new(null, "—") };
        choices.AddRange(session.Roster.Select(player => new SlotChoice(player.Id, player.Name)));

        for (var slot = 0; slot < session.Current.Lineup.Count; slot++)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };

            row.Controls.Add(new Label
            {
                Text = session.SlotLabel(slot),
                AutoSize = false,
                Width = 96,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft
            });

            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            box.Items.AddRange([.. choices.Cast<object>()]);

            var index = slot;
            box.SelectedIndexChanged += (_, _) =>
            {
                if (_loading) return;
                if (box.SelectedItem is SlotChoice choice) _session.SetLineupSlot(index, choice.Id);
            };

            row.Controls.Add(box);
            _slots.Add(box);
            _lineup.Controls.Add(row);
        }
    }

    private static string[] Upper(IEnumerable<string> names) =>
        [.. names.Select(name => name.ToUpperInvariant())];

    private static ScoringState BuildScoringState(Session session)
    {
        var view = session.View();
        return new ScoringState(
            Upper(view.HomeNames), Upper(view.AwayNames),
            view.HomeScore, view.AwayScore,
            view.HomeTarget, view.AwayTarget,
            view.Breaking);
    }

    private static BannerState BuildBannerState(Session session)
    {
        var view = session.View();

        return new BannerState(
            Upper(view.HomeNames), Upper(view.AwayNames),
            view.HomeScore, view.AwayScore,
            view.HomeTarget, view.AwayTarget,
            view.Breaking);
    }

    private void FillLeaders(Session session)
    {
        _leaders.BeginUpdate();
        _leaders.Items.Clear();

        foreach (var row in session.Standings())
        {
            _leaders.Items.Add(new ListViewItem([row.Name, $"{row.Wins}/{row.Played}", row.Possible.ToString()]));
        }

        _leaders.EndUpdate();
    }

    private void FillMatches(Session session)
    {
        _matches.BeginUpdate();
        _matches.Items.Clear();

        // The tab itself is gone in a one-off match, so there is nothing to fill.
        if (!session.IsRoundRobin)
        {
            _matches.EndUpdate();
            return;
        }

        foreach (var match in session.Config.Schedule)
        {
            var score = session.RoundRobin.Scores.First(item => item.Set == match.Set);
            var isCurrent = session.RoundRobin.CurrentSet == match.Set;

            var status = score.Committed ? "Played" : isCurrent ? "Current" : "Waiting";
            var line = score.Committed || score.RacksPlayed > 0 ? $"{score.Home} - {score.Away}" : "-";

            // The dot marks the side breaking first, as the schedule does on the page.
            var first = Breaks.FirstBreakSide(score.BreakSwapped);
            var home = (first == Side.Home ? "• " : "  ") + Names.TeamName(match.Home, session.NameFor);
            var away = (first == Side.Away ? "• " : "  ") + Names.TeamName(match.Away, session.NameFor);

            var item = new ListViewItem([
                match.Set.ToString(), match.Round.ToString(), home, line, away,
                match.Sitting is null ? "-" : session.NameFor(match.Sitting), status
            ]);

            if (isCurrent) item.BackColor = ColorTranslator.FromHtml("#eef2f7");
            _matches.Items.Add(item);
        }

        _matches.EndUpdate();
    }

    private void ToggleBanner()
    {
        if (_banner is not null)
        {
            _banner.Close();
            _banner = null;
            return;
        }

        _banner = new BannerForm { Owner = this, State = BuildBannerState(_session) };
        _banner.FormClosed += (_, _) => _banner = null;
        _banner.Location = new Point(Left, Math.Max(0, Top - _banner.Height - 8));
        _banner.Show();
    }
}
