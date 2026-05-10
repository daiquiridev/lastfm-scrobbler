using LastFmScrobbler.Core;
using LastFmScrobbler.Data;
using LastFmScrobbler.Localization;
using LastFmScrobbler.Models;

namespace LastFmScrobbler.UI;

public class MainForm : Form
{
    private static readonly Color CSidebar = Color.FromArgb(15, 15, 15);
    private static readonly Color CMain    = Color.FromArgb(24, 24, 24);
    private static readonly Color CFg      = Color.FromArgb(220, 220, 220);
    private static readonly Color CDim     = Color.FromArgb(110, 110, 110);
    private static readonly Color CInput   = Color.FromArgb(36, 36, 36);

    private Color _cAccent;

    private readonly Database _db;
    private readonly ScrobbleEngine _engine;
    private AppSettings _settings;

    private NavButton _btnMonitor  = null!;
    private NavButton _btnHistory  = null!;
    private NavButton _btnAccount  = null!;
    private NavButton _btnScrobble = null!;
    private NavButton _btnNorm     = null!;
    private NavButton _btnStats    = null!;
    private NavButton _btnFriends  = null!;
    private NavButton _btnAbout    = null!;

    private Panel _content      = null!;
    private Panel _pageMonitor  = null!;
    private Panel _pageHistory  = null!;
    private Panel _pageAccount  = null!;
    private Panel _pageScrobble = null!;
    private Panel _pageNorm     = null!;
    private Panel _pageStats    = null!;
    private Panel _pageFriends  = null!;
    private Panel _pageAbout    = null!;

    // Monitor page
    private Label       _monTitle      = null!;
    private Label       _monArtist     = null!;
    private Label       _monAlbum      = null!;
    private ProgressBar _monBar        = null!;
    private Label       _monStatus     = null!;
    private Label       _monEta        = null!;
    private LogList     _monLog        = null!;
    private PictureBox  _albumArt      = null!;
    private Button      _loveBtn       = null!;
    private Label       _monQuickToday = null!;
    private Label       _monQuickWeek  = null!;
    private Label       _monQuickTotal = null!;
    private Label       _monQuickStreak = null!;
    private Label       _monGoalBar    = null!;
    private bool        _trackLoved;
    private CancellationTokenSource? _artCts;
    private CancellationTokenSource? _bioCts;

    // Monitor artist bio
    private Panel  _artistInfoPanel = null!;
    private Label  _artistBioText   = null!;
    private FlowLayoutPanel _similarFlow = null!;
    private string _lastBioArtist   = "";

    private ComboBox _langCombo = null!;

    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 500 };
    private Track?   _currentTrack;
    private DateTime _startedAt;
    private int      _threshMs;
    private bool     _scrobbled;

    // Account page
    private Label     _authStatusLabel = null!;
    private Button    _authBtn         = null!;
    private LinkLabel _profileLink     = null!;

    // Scrobbling page
    private NumericUpDown _threshPct    = null!;
    private NumericUpDown _threshMax    = null!;
    private NumericUpDown _dupWindow    = null!;
    private NumericUpDown _goalSpin     = null!;
    private CheckBox _filterAppleChk   = null!;
    private CheckBox _editBeforeChk    = null!;
    private CheckBox _showNotifChk     = null!;
    private CheckBox _startWinChk      = null!;
    private TextBox  _webhookUrlBox    = null!;
    private CheckBox _webhookScrobbleChk    = null!;
    private CheckBox _webhookNowPlayingChk  = null!;

    // Normalization page
    private CheckBox     _autoNormChk = null!;
    private DataGridView _rulesGrid   = null!;

    // History page
    private Label        _statTotal   = null!;
    private Label        _statToday   = null!;
    private Label        _statWeek    = null!;
    private Label        _statPending = null!;
    private DataGridView _historyGrid = null!;

    // Stats page
    private Panel  _chartPanel    = null!;
    private Panel  _artistsPanel  = null!;
    private Panel  _tracksPanel   = null!;
    private Panel  _genrePanel    = null!;
    private Label  _bigTotal      = null!;
    private Label  _bigWeek       = null!;
    private Label  _bigToday      = null!;
    private Label  _statsSourceLbl = null!;
    private string _statsPeriod   = "local";
    private Button? _activePeriodBtn;
    private (string day, int count)[]                  _dailyData      = [];
    private (string artist, int count)[]               _topArtistsData = [];
    private (string artist, string title, int count)[] _topTracksData  = [];
    private (string genre, int count)[]                _genreData      = [];

    // Friends page
    private Panel _friendsListPanel = null!;
    private Label _friendsStatusLbl = null!;

    private Panel      _accentBar  = null!;
    private NavButton? _activeNavBtn;
    private readonly List<Button> _accentBtns = new();

    private Button _maxBtn = null!;
    private bool   _externalCloseRequested;

    public MainForm(Database db, ScrobbleEngine engine, AppSettings settings)
    {
        _db = db; _engine = engine; _settings = settings;
        _cAccent = ColorFromHex(settings.AccentColor, Color.FromArgb(186, 0, 0));

        InitializeComponent();
        BuildMonitorPage();
        BuildHistoryPage();
        BuildAccountPage();
        BuildScrobblePage();
        BuildNormPage();
        BuildStatsPage();
        BuildFriendsPage();
        BuildAboutPage();
        LoadSettings();
        LoadRules();

        _engine.NowPlayingChanged   += OnNowPlaying;
        _engine.TrackScrobbled      += OnScrobbled;
        _engine.PendingQueueFlushed += OnQueueFlushed;
        _tick.Tick += OnTick;
        _tick.Start();

        if (_engine.CurrentTrack is Track t) OnNowPlaying(null, t);
        Navigate(_pageMonitor, _btnMonitor);
        RefreshMonitorStats();
    }

    // ── Shell ─────────────────────────────────────────────────────────────────

    private void InitializeComponent()
    {
        Text            = "Last.fm Scrobbler";
        Size            = new Size(1100, 680);
        MinimumSize     = new Size(900, 560);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor       = CMain;
        ForeColor       = CFg;
        Font            = FontManager.Regular(9.5f);
        FormClosing    += (_, e) =>
        {
            // Hide to tray on user close (X button); allow real shutdown when
            // Windows / Restart Manager / installer asks us to terminate.
            if (e.CloseReason == CloseReason.UserClosing && !_externalCloseRequested)
            {
                e.Cancel = true; Hide();
            }
        };
        SizeChanged    += (_, _) => { if (_maxBtn != null) _maxBtn.Text = WindowState == FormWindowState.Maximized ? "❐" : "□"; };
        Load           += (_, _) => SizePages();

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = CSidebar };

        _btnMonitor  = NavBtn(Loc.T("NavMonitor"),       "▶");
        _btnHistory  = NavBtn(Loc.T("NavHistory"),       "◎");
        _btnStats    = NavBtn(Loc.T("NavStats"),          "∑");
        _btnFriends  = NavBtn("Friends",                  "☆");
        _btnAccount  = NavBtn(Loc.T("NavAccount"),       "◉");
        _btnScrobble = NavBtn(Loc.T("NavScrobbling"),    "⚙");
        _btnNorm     = NavBtn(Loc.T("NavNormalization"), "≡");
        _btnAbout    = NavBtn("About",                   "◈");

        _btnMonitor.Click  += (_, _) => { Navigate(_pageMonitor,  _btnMonitor);  RefreshMonitorStats(); };
        _btnHistory.Click  += (_, _) => { Navigate(_pageHistory,  _btnHistory);  LoadHistory(); RefreshStats(); };
        _btnStats.Click    += (_, _) => { Navigate(_pageStats,    _btnStats);    LoadStatsPage(); };
        _btnFriends.Click  += (_, _) => { Navigate(_pageFriends,  _btnFriends);  _ = RefreshFriendsAsync(); };
        _btnAccount.Click  += (_, _) => Navigate(_pageAccount,  _btnAccount);
        _btnScrobble.Click += (_, _) => Navigate(_pageScrobble, _btnScrobble);
        _btnNorm.Click     += (_, _) => Navigate(_pageNorm,     _btnNorm);
        _btnAbout.Click    += (_, _) => Navigate(_pageAbout,    _btnAbout);

        var bugBtn = new Button
        {
            Text      = Loc.T("ReportBug"),
            Dock      = DockStyle.Bottom,
            Height    = 36,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(60, 60, 60),
            BackColor = Color.Transparent,
            Font      = FontManager.Regular(8f),
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        bugBtn.FlatAppearance.BorderSize = 0;
        bugBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 22, 22);
        bugBtn.MouseEnter += (_, _) => bugBtn.ForeColor = Color.FromArgb(110, 110, 110);
        bugBtn.MouseLeave += (_, _) => bugBtn.ForeColor = Color.FromArgb(60, 60, 60);
        bugBtn.Click += (_, _) => OpenUrl("mailto:support@spacechild.dev?subject=Last.fm%20Scrobbler%20Bug");

        sidebar.Controls.Add(bugBtn);
        sidebar.Controls.Add(_btnAbout);
        sidebar.Controls.Add(_btnNorm);
        sidebar.Controls.Add(_btnScrobble);
        sidebar.Controls.Add(_btnAccount);
        sidebar.Controls.Add(_btnFriends);
        sidebar.Controls.Add(_btnStats);
        sidebar.Controls.Add(_btnHistory);
        sidebar.Controls.Add(_btnMonitor);

        _content = new Panel { Dock = DockStyle.Fill, BackColor = CMain };

        _pageMonitor  = new Panel { BackColor = CMain, Visible = false, Padding = new Padding(24, 16, 24, 12) };
        _pageHistory  = new Panel { BackColor = CMain, Visible = false };
        _pageAccount  = new Panel { BackColor = CMain, Visible = false };
        _pageScrobble = new Panel { BackColor = CMain, Visible = false };
        _pageNorm     = new Panel { BackColor = CMain, Visible = false };
        _pageStats    = new Panel { BackColor = CMain, Visible = false };
        _pageFriends  = new Panel { BackColor = CMain, Visible = false };
        _pageAbout    = new Panel { BackColor = CMain, Visible = false };

        _content.Controls.AddRange([_pageMonitor, _pageHistory, _pageAccount, _pageScrobble, _pageNorm, _pageStats, _pageFriends, _pageAbout]);
        _content.Resize += (_, _) => SizePages();

        var titleBar = BuildTitleBar();

        Controls.Add(_content);
        Controls.Add(sidebar);
        Controls.Add(titleBar);
    }

    private void SizePages()
    {
        var r = _content.ClientRectangle;
        foreach (var p in new[] { _pageMonitor, _pageHistory, _pageAccount, _pageScrobble, _pageNorm, _pageStats, _pageFriends, _pageAbout })
            p.Bounds = r;
    }

    private void Navigate(Panel page, NavButton btn)
    {
        foreach (var p in new[] { _pageMonitor, _pageHistory, _pageAccount, _pageScrobble, _pageNorm, _pageStats, _pageFriends, _pageAbout })
            p.Visible = false;
        foreach (var b in new[] { _btnMonitor, _btnHistory, _btnStats, _btnFriends, _btnAccount, _btnScrobble, _btnNorm, _btnAbout })
        { b.BackColor = Color.Transparent; b.ForeColor = CDim; }
        page.Visible  = true;
        btn.BackColor = _cAccent;
        btn.ForeColor = Color.White;
        _activeNavBtn = btn;
    }

    public void ShowMonitor()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        Navigate(_pageMonitor, _btnMonitor);
        RefreshMonitorStats();
    }

    public void ShowSettings()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        Navigate(_pageAccount, _btnAccount);
    }

    public void ToggleLoveCurrentTrack()
    {
        if (_loveBtn.Enabled) _loveBtn.PerformClick();
    }

    // ── Monitor Page ──────────────────────────────────────────────────────────

    private void BuildMonitorPage()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 160, BackColor = Color.FromArgb(28, 28, 28) };

        _accentBar = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = _cAccent };
        _albumArt  = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Transparent };
        var artWrap = new Panel { Dock = DockStyle.Right, Width = 160, BackColor = Color.FromArgb(20, 20, 20), Padding = new Padding(6) };
        artWrap.Controls.Add(_albumArt);

        var cardInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(18, 14, 14, 10) };
        var nowLbl    = new Label { Dock = DockStyle.Top, Height = 18, Text = Loc.T("NowPlaying"), Font = FontManager.Bold(7.5f), ForeColor = Color.FromArgb(80, 80, 80), TextAlign = ContentAlignment.MiddleLeft };
        _monTitle  = new Label { Dock = DockStyle.Top, Height = 40, Font = FontManager.Bold(17f),   ForeColor = CFg,                           Text = "—", AutoEllipsis = true };
        _monArtist = new Label { Dock = DockStyle.Top, Height = 28, Font = FontManager.Regular(11f), ForeColor = Color.FromArgb(175, 175, 175), Text = "",  AutoEllipsis = true };
        _monAlbum  = new Label { Dock = DockStyle.Top, Height = 22, Font = FontManager.Italic(9f),   ForeColor = Color.FromArgb(85, 85, 85),    Text = "",  AutoEllipsis = true };
        cardInner.Controls.Add(_monAlbum);
        cardInner.Controls.Add(_monArtist);
        cardInner.Controls.Add(_monTitle);
        cardInner.Controls.Add(nowLbl);
        card.Controls.Add(cardInner);
        card.Controls.Add(artWrap);
        card.Controls.Add(_accentBar);

        // Status row
        var statusRow = new Panel { Dock = DockStyle.Top, Height = 26 };
        _loveBtn = new Button
        {
            Text = "♡", Dock = DockStyle.Right, Width = 32, FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(100, 100, 100), BackColor = Color.Transparent,
            Font = FontManager.Regular(12f), Cursor = Cursors.Hand, Enabled = false,
            UseVisualStyleBackColor = false,
        };
        _loveBtn.FlatAppearance.BorderSize = 0;
        _loveBtn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        _loveBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;
        _loveBtn.Click += LoveBtnClicked;
        _monStatus = new Label { Dock = DockStyle.Fill, Font = FontManager.Regular(9f), ForeColor = CDim, Text = Loc.T("NotPlaying"), TextAlign = ContentAlignment.MiddleLeft };
        _monEta    = new Label { Dock = DockStyle.Right, Width = 58, TextAlign = ContentAlignment.MiddleRight, Font = FontManager.Regular(8.5f), ForeColor = CDim };
        statusRow.Controls.Add(_monStatus);
        statusRow.Controls.Add(_monEta);
        statusRow.Controls.Add(_loveBtn);

        var barRow = new Panel { Dock = DockStyle.Top, Height = 6 };
        _monBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, ForeColor = _cAccent };
        barRow.Controls.Add(_monBar);

        // Quick stats strip (4 columns: today, week, total, streak)
        var statsStrip = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.FromArgb(20, 20, 20) };
        _monQuickToday  = BigStatLabel(24);
        _monQuickWeek   = BigStatLabel(224);
        _monQuickTotal  = BigStatLabel(424);
        _monQuickStreak = BigStatLabel(624);
        var l1 = SmallStatLabel(Loc.T("StatsToday").ToUpperInvariant(),      24);
        var l2 = SmallStatLabel(Loc.T("StatsThisWeek").ToUpperInvariant(),   224);
        var l3 = SmallStatLabel(Loc.T("StatsTotalLabel").ToUpperInvariant(), 424);
        var l4 = SmallStatLabel("STREAK",                                    624);
        statsStrip.Controls.AddRange([_monQuickToday, _monQuickWeek, _monQuickTotal, _monQuickStreak, l1, l2, l3, l4]);

        // Goal bar (visible only when goal > 0)
        _monGoalBar = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 22,
            Font      = FontManager.Regular(8.5f),
            ForeColor = CDim,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(2, 0, 0, 0),
            Visible   = false,
            BackColor = Color.FromArgb(20, 20, 20),
        };

        // Bottom: log (left) + artist bio (right)
        var bottomSplit  = new Panel { Dock = DockStyle.Fill };
        var line         = new Label { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(35, 35, 35) };
        var logLbl       = SectionLabel("LOG");

        _monLog = new LogList
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 18, 18),
            Accent    = _cAccent,
        };

        var logCol = new Panel { Dock = DockStyle.Fill, BackColor = CMain };
        logCol.Controls.Add(_monLog);
        logCol.Controls.Add(logLbl);
        logCol.Controls.Add(line);

        // Artist info panel (right side, 300px)
        _artistInfoPanel = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = Color.FromArgb(20, 20, 20), Padding = new Padding(16, 10, 16, 10), Visible = false };
        var bioHdr = SectionLabel("ARTIST");
        _artistBioText = new Label
        {
            Dock = DockStyle.Top, AutoSize = false, Height = 90,
            Font = FontManager.Regular(8.5f), ForeColor = Color.FromArgb(140, 140, 140),
            Text = "", AutoEllipsis = false,
        };
        var simHdr = SectionLabel("SIMILAR ARTISTS");
        _similarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 80, AutoSize = false,
            WrapContents = true, BackColor = Color.Transparent,
        };
        _artistInfoPanel.Controls.Add(_similarFlow);
        _artistInfoPanel.Controls.Add(simHdr);
        _artistInfoPanel.Controls.Add(_artistBioText);
        _artistInfoPanel.Controls.Add(bioHdr);

        var divider = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Color.FromArgb(35, 35, 35) };

        bottomSplit.Controls.Add(logCol);
        bottomSplit.Controls.Add(divider);
        bottomSplit.Controls.Add(_artistInfoPanel);

        _pageMonitor.Controls.Add(bottomSplit);
        _pageMonitor.Controls.Add(_monGoalBar);
        _pageMonitor.Controls.Add(statsStrip);
        _pageMonitor.Controls.Add(Gap(14));
        _pageMonitor.Controls.Add(barRow);
        _pageMonitor.Controls.Add(statusRow);
        _pageMonitor.Controls.Add(Gap(12));
        _pageMonitor.Controls.Add(card);
    }

    private Label BigStatLabel(int x) => new()
    {
        Text = "—", Font = FontManager.Bold(18f), ForeColor = CFg,
        Location = new Point(x, 6), Size = new Size(190, 26), AutoEllipsis = true,
    };

    private static Label SmallStatLabel(string text, int x) => new()
    {
        Text = text, Font = FontManager.Bold(7f), ForeColor = Color.FromArgb(65, 65, 65),
        Location = new Point(x, 36), Size = new Size(190, 14),
    };

    private void RefreshMonitorStats()
    {
        if (InvokeRequired) { Invoke(RefreshMonitorStats); return; }
        var (total, today, week) = _db.GetStats();
        var streak = _db.GetCurrentStreak();
        _monQuickToday.Text  = today.ToString("N0");
        _monQuickWeek.Text   = week.ToString("N0");
        _monQuickTotal.Text  = total.ToString("N0");
        _monQuickStreak.Text = streak > 0 ? $"{streak}d" : "—";

        if (_settings.DailyScrobbleGoal > 0)
        {
            _monGoalBar.Visible = true;
            int pct = Math.Min(100, today * 100 / _settings.DailyScrobbleGoal);
            _monGoalBar.Text = $"  Daily goal: {today} / {_settings.DailyScrobbleGoal}  ({pct}%)";
        }
        else _monGoalBar.Visible = false;
    }

    // ── History Page ──────────────────────────────────────────────────────────

    private void BuildHistoryPage()
    {
        var heading = PageHeading(Loc.T("NavHistory"));
        heading.Dock = DockStyle.Top;

        var statsRow = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(20, 20, 20) };
        _statTotal   = StatLabel(string.Format(Loc.T("StatTotal"),   "—"));
        _statToday   = StatLabel(string.Format(Loc.T("StatToday"),   "—"));
        _statWeek    = StatLabel(string.Format(Loc.T("StatWeek"),    "—"));
        _statPending = StatLabel(Loc.T("StatQueueEmpty"));
        _statTotal.Location   = new Point(24,  12);
        _statToday.Location   = new Point(200, 12);
        _statWeek.Location    = new Point(370, 12);
        _statPending.Location = new Point(540, 12);
        statsRow.Controls.AddRange([_statTotal, _statToday, _statWeek, _statPending]);

        _historyGrid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(20, 20, 20), GridColor = Color.FromArgb(35, 35, 35),
            BorderStyle = BorderStyle.None, ForeColor = CFg, EnableHeadersVisualStyles = false,
            MultiSelect = false, Font = FontManager.Regular(9f),
        };
        _historyGrid.DefaultCellStyle.BackColor          = Color.FromArgb(26, 26, 26);
        _historyGrid.DefaultCellStyle.ForeColor          = CFg;
        _historyGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 45, 45);
        _historyGrid.DefaultCellStyle.SelectionForeColor = CFg;
        _historyGrid.DefaultCellStyle.Padding            = new Padding(0, 3, 0, 3);
        _historyGrid.AlternatingRowsDefaultCellStyle.BackColor        = Color.FromArgb(22, 22, 22);
        _historyGrid.ColumnHeadersDefaultCellStyle.BackColor          = Color.FromArgb(18, 18, 18);
        _historyGrid.ColumnHeadersDefaultCellStyle.ForeColor          = Color.FromArgb(90, 90, 90);
        _historyGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 18, 18);
        _historyGrid.ColumnHeadersDefaultCellStyle.Font               = FontManager.Bold(8f);
        _historyGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _historyGrid.ColumnHeadersHeight = 30;
        _historyGrid.RowTemplate.Height  = 28;
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time",   HeaderText = Loc.T("ColTime"),   FillWeight = 14 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Artist", HeaderText = Loc.T("ColArtist"), FillWeight = 25 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title",  HeaderText = Loc.T("ColTrack"),  FillWeight = 30 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Album",  HeaderText = Loc.T("ColAlbum"),  FillWeight = 26 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = Loc.T("ColStatus"), FillWeight = 5  });
        if (_historyGrid.Columns["Id"] is null)
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });

        // Right-click context menu
        var ctxMenu = new ContextMenuStrip { BackColor = Color.FromArgb(30, 30, 30), ForeColor = CFg };
        var editItem = new ToolStripMenuItem("Edit & Rescrobble");
        editItem.Click += EditScrobbleClicked;
        ctxMenu.Items.Add(editItem);
        _historyGrid.ContextMenuStrip = ctxMenu;
        _historyGrid.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _historyGrid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0) _historyGrid.Rows[hit.RowIndex].Selected = true;
        };

        var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = CMain };
        var refreshBtn = MakeBtn(Loc.T("BtnRefresh"), 110, 30);
        refreshBtn.Location = new Point(24, 8);
        refreshBtn.Click   += (_, _) => { LoadHistory(); RefreshStats(); };

        var manualBtn = MakeBtn(Loc.T("BtnManualScrobble"), 160, 30);
        manualBtn.Location  = new Point(144, 8);
        manualBtn.Click    += ManualScrobbleClicked;

        var exportBtn = MakeBtn("Export CSV", 100, 30);
        exportBtn.Location  = new Point(318, 8);
        exportBtn.Click    += ExportCsvClicked;

        var importBtn = MakeBtn("Import CSV", 100, 30);
        importBtn.Location  = new Point(428, 8);
        importBtn.Click    += ImportCsvClicked;

        btnRow.Controls.AddRange([refreshBtn, manualBtn, exportBtn, importBtn]);

        _pageHistory.Controls.Add(_historyGrid);
        _pageHistory.Controls.Add(statsRow);
        _pageHistory.Controls.Add(heading);
        _pageHistory.Controls.Add(btnRow);
    }

    private void LoadHistory()
    {
        if (InvokeRequired) { Invoke(LoadHistory); return; }
        _historyGrid.Rows.Clear();
        foreach (var rec in _db.LoadHistory(200))
        {
            var status = rec.Success ? "✓" : "✗";
            var i = _historyGrid.Rows.Add(
                rec.ScrobbledAt.ToLocalTime().ToString("MM-dd HH:mm"),
                rec.Artist, rec.Title, rec.Album, status);
            _historyGrid.Rows[i].Tag = rec.Id;
            _historyGrid.Rows[i].DefaultCellStyle.ForeColor =
                rec.Success ? CFg : Color.FromArgb(180, 60, 60);
        }
    }

    private void RefreshStats()
    {
        if (InvokeRequired) { Invoke(RefreshStats); return; }
        var (total, today, week) = _db.GetStats();
        var pending = _db.PendingCount();
        _statTotal.Text   = string.Format(Loc.T("StatTotal"), total.ToString("N0"));
        _statToday.Text   = string.Format(Loc.T("StatToday"), today.ToString("N0"));
        _statWeek.Text    = string.Format(Loc.T("StatWeek"),  week.ToString("N0"));
        _statPending.Text = pending > 0 ? string.Format(Loc.T("StatQueue"), pending) : Loc.T("StatQueueEmpty");
    }

    private void EditScrobbleClicked(object? sender, EventArgs e)
    {
        if (_historyGrid.SelectedRows.Count == 0) return;
        var row = _historyGrid.SelectedRows[0];
        if (row.Tag is not int id) return;

        var artist = row.Cells["Artist"].Value?.ToString() ?? "";
        var title  = row.Cells["Title"].Value?.ToString()  ?? "";
        var album  = row.Cells["Album"].Value?.ToString()  ?? "";

        var fakeTrack = new Track { Artist = artist, Title = title, Album = album };
        using var dlg = new EditTrackForm(fakeTrack);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _db.UpdateScrobbleRecord(id, fakeTrack.Artist, fakeTrack.Title, fakeTrack.Album);
        _ = _engine.ManualScrobbleAsync(fakeTrack.Artist, fakeTrack.Title, fakeTrack.Album, DateTime.Now);
        LoadHistory();
        AppendLog(LogKind.Manual, $"{fakeTrack.Artist} — {fakeTrack.Title}");
    }

    private void ExportCsvClicked(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title    = "Export Scrobbles",
            Filter   = "CSV files (*.csv)|*.csv",
            FileName = $"scrobbles_{DateTime.Today:yyyy-MM-dd}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _db.ExportToCsv(dlg.FileName);
        MessageBox.Show($"Exported to {dlg.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportCsvClicked(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Title = "Import Scrobbles", Filter = "CSV files (*.csv)|*.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        int count = _db.ImportFromCsv(dlg.FileName);
        LoadHistory();
        RefreshStats();
        RefreshMonitorStats();
        MessageBox.Show($"Imported {count} new scrobbles.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Account Page ──────────────────────────────────────────────────────────

    private void BuildAccountPage()
    {
        const int lx = 24;
        int y = 16;

        _authStatusLabel = new Label { Size = new Size(540, 24), ForeColor = CDim, Font = FontManager.Regular(9f) };

        _profileLink = new LinkLabel { Size = new Size(240, 20), Font = FontManager.Regular(9f), LinkColor = _cAccent, ForeColor = CDim, Visible = false };
        _profileLink.LinkClicked += (_, _) => { if (!string.IsNullOrEmpty(_settings.Username)) OpenUrl($"https://www.last.fm/user/{_settings.Username}"); };

        _authBtn = MakeBtn(Loc.T("LoginWithLastFm"), 170, 32);
        _authBtn.Click += AuthClicked;

        var heading = PageHeading(Loc.T("NavAccount"));
        heading.Dock = DockStyle.Top;

        var inner = new Panel { Dock = DockStyle.Fill, BackColor = CMain };

        _authStatusLabel.Location = new Point(lx, y); inner.Controls.Add(_authStatusLabel); y += 30;
        _profileLink.Location     = new Point(lx, y); inner.Controls.Add(_profileLink);     y += 32;
        _authBtn.Location         = new Point(lx, y); inner.Controls.Add(_authBtn);

        _pageAccount.Controls.Add(inner);
        _pageAccount.Controls.Add(heading);
    }

    // ── Scrobbling Page ───────────────────────────────────────────────────────

    private void BuildScrobblePage()
    {
        const int lx = 24, rx = 220;
        int y = 16;

        _threshPct = new NumericUpDown { Size = new Size(68, 26), Minimum = 10, Maximum = 100, Value = 50,  Increment = 5,  BackColor = CInput, ForeColor = CFg, Font = FontManager.Regular(9.5f) };
        _threshMax = new NumericUpDown { Size = new Size(68, 26), Minimum = 30, Maximum = 600, Value = 240, Increment = 30, BackColor = CInput, ForeColor = CFg, Font = FontManager.Regular(9.5f) };
        _dupWindow = new NumericUpDown { Size = new Size(68, 26), Minimum = 0,  Maximum = 60,  Value = 5,   Increment = 1,  BackColor = CInput, ForeColor = CFg, Font = FontManager.Regular(9.5f) };
        _goalSpin  = new NumericUpDown { Size = new Size(68, 26), Minimum = 0,  Maximum = 999, Value = 0,   Increment = 5,  BackColor = CInput, ForeColor = CFg, Font = FontManager.Regular(9.5f) };

        _filterAppleChk = MakeChk(Loc.T("FilterAppleOnly"));
        _editBeforeChk  = MakeChk(Loc.T("EditBeforeScrobble"));
        _showNotifChk   = MakeChk(Loc.T("ShowNotification"));
        _startWinChk    = MakeChk(Loc.T("StartWithWindows"));

        _langCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CInput, ForeColor = CFg, FlatStyle = FlatStyle.Flat, Size = new Size(160, 26), Font = FontManager.Regular(9.5f) };
        foreach (var l in Loc.Available) _langCombo.Items.Add(l.NativeName);

        var saveBtn = MakeBtn(Loc.T("Save"), 86, 32);
        saveBtn.BackColor = _cAccent; _accentBtns.Add(saveBtn);
        saveBtn.Click += (_, _) => SaveScrobbleSettings();

        var accentBtn = MakeBtn(Loc.T("PickColor"), 100, 32);
        accentBtn.Click += PickAccentColorClicked;

        var heading = PageHeading(Loc.T("NavScrobbling"));
        heading.Dock = DockStyle.Top;

        var inner = new Panel { Dock = DockStyle.Fill, BackColor = CMain };

        void NumRow(string lbl, NumericUpDown ctrl, string unit)
        {
            inner.Controls.Add(RowLabel(lbl, lx, y + 5));
            ctrl.Location = new Point(rx, y);
            inner.Controls.Add(ctrl);
            inner.Controls.Add(new Label { Text = unit, Location = new Point(rx + 74, y + 5), Size = new Size(280, 20), ForeColor = CDim, Font = FontManager.Regular(9f) });
            y += 36;
        }

        NumRow(Loc.T("LblThreshold"), _threshPct, Loc.T("UnitAfterPercent"));
        NumRow(Loc.T("LblMaximum"),   _threshMax, Loc.T("UnitSecondsFirst"));

        inner.Controls.Add(new Label { Text = Loc.T("NoteMin30"), Location = new Point(lx, y), Size = new Size(540, 18), ForeColor = Color.FromArgb(60, 60, 60), Font = FontManager.Regular(8.5f) });
        y += 28;

        NumRow(Loc.T("LblDuplicateProtection"), _dupWindow, Loc.T("UnitMinSkipDup"));
        NumRow("Daily scrobble goal", _goalSpin, "scrobbles/day  (0 = disabled)");

        foreach (var chk in new CheckBox[] { _filterAppleChk, _editBeforeChk, _showNotifChk, _startWinChk })
        {
            chk.Location = new Point(lx, y); inner.Controls.Add(chk); y += 30;
        }

        saveBtn.Location   = new Point(lx, y + 10);
        accentBtn.Location = new Point(lx + saveBtn.Width + 12, y + 10);
        inner.Controls.Add(saveBtn);
        inner.Controls.Add(accentBtn);
        inner.Controls.Add(new Label { Text = Loc.T("LblAccentColor"), Location = new Point(lx + saveBtn.Width + accentBtn.Width + 22, y + 17), Size = new Size(100, 18), ForeColor = CDim, Font = FontManager.Regular(9f) });

        y += 50;
        inner.Controls.Add(new Label { Text = Loc.T("LblLanguage"), Location = new Point(lx, y + 5), Size = new Size(190, 20), ForeColor = CDim, Font = FontManager.Regular(9f) });
        _langCombo.Location = new Point(220, y);
        inner.Controls.Add(_langCombo);

        // Hotkeys info
        y += 40;
        inner.Controls.Add(new Label
        {
            Text = "Hotkeys:  Ctrl+Alt+L  →  Love/unlove track      Ctrl+Alt+C  →  Copy now playing",
            Location = new Point(lx, y), Size = new Size(600, 20), ForeColor = Color.FromArgb(70, 70, 70), Font = FontManager.Regular(8.5f),
        });

        // Webhook section
        y += 32;
        inner.Controls.Add(new Label
        {
            Text = "WEBHOOKS", Location = new Point(lx, y), Size = new Size(300, 18),
            ForeColor = Color.FromArgb(70, 70, 70), Font = FontManager.Bold(8f),
        });
        y += 22;

        _webhookUrlBox = new TextBox
        {
            Location    = new Point(lx, y),
            Size        = new Size(500, 26),
            BackColor   = CInput,
            ForeColor   = CFg,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = FontManager.Regular(9.5f),
            PlaceholderText = "https://your-webhook-url",
        };
        inner.Controls.Add(_webhookUrlBox);
        y += 34;

        _webhookScrobbleChk   = MakeChk("Post on scrobble");
        _webhookNowPlayingChk = MakeChk("Post on now playing / stopped");

        foreach (var chk in new CheckBox[] { _webhookScrobbleChk, _webhookNowPlayingChk })
        {
            chk.Location = new Point(lx, y); inner.Controls.Add(chk); y += 28;
        }

        _pageScrobble.Controls.Add(inner);
        _pageScrobble.Controls.Add(heading);
    }

    // ── About Page ───────────────────────────────────────────────────────────

    private void BuildAboutPage()
    {
        var heading = PageHeading("About");
        heading.Dock = DockStyle.Top;

        var inner = new Panel { Dock = DockStyle.Fill, BackColor = CMain, AutoScroll = true };

        const int lx = 28;
        int y = 24;

        // ── Hero card ────────────────────────────────────────────────────────
        var heroCard = new Panel
        {
            Location  = new Point(lx, y),
            Size      = new Size(560, 92),
            BackColor = Color.FromArgb(19, 19, 19),
        };
        heroCard.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(36, 36, 36));
            e.Graphics.DrawRectangle(pen, 0, 0, heroCard.Width - 1, heroCard.Height - 1);
        };

        var noteLabel = new Label
        {
            Text      = "♪",
            Location  = new Point(20, 14),
            Size      = new Size(58, 64),
            Font      = FontManager.Bold(30f),
            ForeColor = _cAccent,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var appNameLabel = new Label
        {
            Text      = "Last.fm Scrobbler",
            Location  = new Point(88, 16),
            Size      = new Size(340, 32),
            Font      = FontManager.Bold(17f),
            ForeColor = CFg,
        };

        var versionLabel = new Label
        {
            Text      = $"v{Core.UpdateChecker.DisplayVersion}",
            Location  = new Point(89, 52),
            Size      = new Size(72, 22),
            Font      = FontManager.Regular(8.5f),
            ForeColor = Color.FromArgb(90, 90, 90),
        };

        var licenseLabel = new Label
        {
            Text      = "Open source  ·  MIT License",
            Location  = new Point(170, 54),
            Size      = new Size(240, 18),
            Font      = FontManager.Regular(8.5f),
            ForeColor = Color.FromArgb(55, 55, 55),
        };

        heroCard.Controls.AddRange([noteLabel, appNameLabel, versionLabel, licenseLabel]);
        inner.Controls.Add(heroCard);
        y += heroCard.Height + 20;

        // ── Description ──────────────────────────────────────────────────────
        inner.Controls.Add(new Label
        {
            Text      = "Automatic Last.fm scrobbling for Apple Music on Windows.\r\nUses the Windows SMTC API — the same interface Windows uses for its media overlay (Win+K).\r\nWorks with any SMTC-compatible media player including Spotify, VLC, and others.",
            Location  = new Point(lx, y),
            Size      = new Size(660, 58),
            Font      = FontManager.Regular(9.5f),
            ForeColor = Color.FromArgb(130, 130, 130),
        });
        y += 74;

        // ── Separator ────────────────────────────────────────────────────────
        inner.Controls.Add(new Panel { Location = new Point(lx, y), Size = new Size(620, 1), BackColor = Color.FromArgb(30, 30, 30) });
        y += 18;

        // ── Action buttons ───────────────────────────────────────────────────
        var btnDonate = AboutActionBtn(
            "♥  Donate", _cAccent,
            Color.Transparent,
            LightenColor(_cAccent, 18),
            Color.White,
            () => OpenUrl("https://spacechild.dev/donate"));
        btnDonate.Location = new Point(lx, y);

        var btnGitHub = AboutActionBtn(
            "★  GitHub", Color.FromArgb(26, 26, 26),
            Color.FromArgb(48, 48, 48),
            Color.FromArgb(34, 34, 34),
            CFg,
            () => OpenUrl("https://github.com/SpaceChildDev/lastfm-scrobbler"));
        btnGitHub.Location = new Point(lx + 182, y);

        var btnSite = AboutActionBtn(
            "↗  spacechild.dev", Color.Transparent,
            Color.FromArgb(38, 38, 38),
            Color.FromArgb(22, 22, 22),
            Color.FromArgb(110, 110, 110),
            () => OpenUrl("https://spacechild.dev"));
        btnSite.Location = new Point(lx + 364, y);

        inner.Controls.AddRange([btnDonate, btnGitHub, btnSite]);
        y += 42 + 20;

        // ── Separator ────────────────────────────────────────────────────────
        inner.Controls.Add(new Panel { Location = new Point(lx, y), Size = new Size(620, 1), BackColor = Color.FromArgb(30, 30, 30) });
        y += 18;

        // ── Version section ──────────────────────────────────────────────────
        inner.Controls.Add(new Label
        {
            Text      = "INSTALLED VERSION",
            Location  = new Point(lx, y),
            Size      = new Size(300, 16),
            Font      = FontManager.Bold(7.5f),
            ForeColor = Color.FromArgb(60, 60, 60),
        });
        y += 22;

        inner.Controls.Add(new Label
        {
            Text      = $"v{Core.UpdateChecker.DisplayVersion}",
            Location  = new Point(lx, y),
            Size      = new Size(100, 22),
            Font      = FontManager.Regular(10f),
            ForeColor = Color.FromArgb(180, 180, 180),
        });

        var historyBtn = AboutActionBtn(
            "⊞  Version History", Color.FromArgb(24, 24, 24),
            Color.FromArgb(42, 42, 42),
            Color.FromArgb(30, 30, 30),
            Color.FromArgb(150, 150, 150),
            () =>
            {
                using var form = new VersionHistoryForm(new UpdateChecker(), _cAccent);
                form.ShowDialog(this);
            });
        historyBtn.Location = new Point(lx + 116, y - 8);
        inner.Controls.Add(historyBtn);
        y += 50;

        // ── Separator ────────────────────────────────────────────────────────
        inner.Controls.Add(new Panel { Location = new Point(lx, y), Size = new Size(620, 1), BackColor = Color.FromArgb(26, 26, 26) });
        y += 14;

        // ── Footer ───────────────────────────────────────────────────────────
        inner.Controls.Add(new Label
        {
            Text      = $"© {DateTime.Now.Year} SpaceChild.dev  ·  MIT License  ·  Made with ♥ for music lovers",
            Location  = new Point(lx, y),
            Size      = new Size(600, 18),
            Font      = FontManager.Regular(8.5f),
            ForeColor = Color.FromArgb(48, 48, 48),
        });

        _pageAbout.Controls.Add(inner);
        _pageAbout.Controls.Add(heading);
    }

    private Panel AboutActionBtn(string label, Color fill, Color border, Color hover, Color fg, Action onClick)
    {
        bool hot = false;
        var p = new Panel { Size = new Size(172, 38), Cursor = Cursors.Hand, BackColor = CMain };

        p.Paint += (_, e) =>
        {
            var g  = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
            using var path = RoundedPath(rc, 5);
            using (var b = new SolidBrush(hot ? hover : fill)) g.FillPath(b, path);
            if (border != Color.Transparent)
                using (var pen = new Pen(border)) g.DrawPath(pen, path);
            using (var tb = new SolidBrush(fg))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(label, FontManager.Regular(9.5f), tb, new RectangleF(0, 0, p.Width, p.Height), sf);
            }
        };

        p.MouseEnter += (_, _) => { hot = true;  p.Invalidate(); };
        p.MouseLeave += (_, _) => { hot = false; p.Invalidate(); };
        p.Click      += (_, _) => onClick();
        return p;
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        int d    = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X,         r.Y,          d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        path.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color LightenColor(Color c, int amount) =>
        Color.FromArgb(
            Math.Min(255, c.R + amount),
            Math.Min(255, c.G + amount),
            Math.Min(255, c.B + amount));

    // ── Normalization Page ────────────────────────────────────────────────────

    private void BuildNormPage()
    {
        var heading = PageHeading(Loc.T("NavNormalization"));
        heading.Dock = DockStyle.Top;

        _autoNormChk = new CheckBox
        {
            Text = Loc.T("AutoNormalize"), Dock = DockStyle.Top, Height = 32, AutoSize = false,
            Padding = new Padding(24, 6, 0, 0), ForeColor = CFg, Font = FontManager.Regular(9.5f),
        };

        _rulesGrid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(20, 20, 20), GridColor = Color.FromArgb(35, 35, 35),
            BorderStyle = BorderStyle.None, ForeColor = CFg, EnableHeadersVisualStyles = false,
            Font = FontManager.Regular(9f),
        };
        _rulesGrid.DefaultCellStyle.BackColor          = Color.FromArgb(26, 26, 26);
        _rulesGrid.DefaultCellStyle.ForeColor          = CFg;
        _rulesGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 45, 45);
        _rulesGrid.DefaultCellStyle.SelectionForeColor = CFg;
        _rulesGrid.DefaultCellStyle.Padding            = new Padding(0, 3, 0, 3);
        _rulesGrid.AlternatingRowsDefaultCellStyle.BackColor        = Color.FromArgb(22, 22, 22);
        _rulesGrid.ColumnHeadersDefaultCellStyle.BackColor          = Color.FromArgb(18, 18, 18);
        _rulesGrid.ColumnHeadersDefaultCellStyle.ForeColor          = Color.FromArgb(90, 90, 90);
        _rulesGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(18, 18, 18);
        _rulesGrid.ColumnHeadersDefaultCellStyle.Font               = FontManager.Bold(8f);
        _rulesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _rulesGrid.ColumnHeadersHeight = 30;
        _rulesGrid.RowTemplate.Height  = 28;
        _rulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = Loc.T("ColEnabled"),     FillWeight = 5,  Width = 44 });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn  { Name = "Field",   HeaderText = Loc.T("ColField"),       FillWeight = 10, ReadOnly = true });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn  { Name = "Desc",    HeaderText = Loc.T("ColDescription"), FillWeight = 28, ReadOnly = true });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn  { Name = "Pattern", HeaderText = "Pattern",               FillWeight = 38 });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn  { Name = "Replace", HeaderText = Loc.T("ColReplace"),     FillWeight = 19 });

        var addBtn  = MakeBtn(Loc.T("BtnAddRule"),        116, 30); addBtn.Click  += AddRuleClicked;
        var delBtn  = MakeBtn(Loc.T("BtnDeleteSelected"), 116, 30); delBtn.Click  += DeleteRuleClicked;
        var saveBtn = MakeBtn(Loc.T("Save"), 86, 30);
        saveBtn.BackColor = _cAccent; _accentBtns.Add(saveBtn);
        saveBtn.Click += (_, _) => SaveNormSettings();

        var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = CMain };
        addBtn.Location  = new Point(24,  7);
        delBtn.Location  = new Point(150, 7);
        saveBtn.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
        saveBtn.Location = new Point(790, 7);
        btnRow.Controls.AddRange([addBtn, delBtn, saveBtn]);

        _pageNorm.Controls.Add(_rulesGrid);
        _pageNorm.Controls.Add(_autoNormChk);
        _pageNorm.Controls.Add(heading);
        _pageNorm.Controls.Add(btnRow);
    }

    // ── Stats Page ────────────────────────────────────────────────────────────

    private void BuildStatsPage()
    {
        var heading = PageHeading(Loc.T("NavStats"));
        heading.Dock   = DockStyle.Top;
        heading.Height = 48;

        // Period selector row
        var periodRow = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(18, 18, 18) };
        _statsSourceLbl = new Label
        {
            Dock = DockStyle.Right, Width = 160, TextAlign = ContentAlignment.MiddleRight,
            Font = FontManager.Regular(8f), ForeColor = Color.FromArgb(70, 70, 70),
            Padding = new Padding(0, 0, 16, 0),
        };

        string[] periodLabels = ["Local", "7 days", "1 month", "3 months", "6 months", "1 year", "All time"];
        string[] periodValues = ["local", "7day",   "1month",  "3month",   "6month",   "12month", "overall"];

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(16, 6, 0, 0) };
        for (int i = 0; i < periodLabels.Length; i++)
        {
            var btn = new Button
            {
                Text = periodLabels[i], Size = new Size(i == 0 ? 56 : 72, 26),
                FlatStyle = FlatStyle.Flat, Font = FontManager.Regular(8.5f),
                BackColor = i == 0 ? _cAccent : Color.FromArgb(30, 30, 30),
                ForeColor = i == 0 ? Color.White : CDim,
                Cursor = Cursors.Hand, Margin = new Padding(0, 0, 4, 0),
                UseVisualStyleBackColor = false, Tag = periodValues[i],
            };
            btn.FlatAppearance.BorderSize = 0;
            if (i == 0) { _activePeriodBtn = btn; _accentBtns.Add(btn); }
            btn.Click += PeriodBtnClicked;
            flow.Controls.Add(btn);
        }

        periodRow.Controls.Add(_statsSourceLbl);
        periodRow.Controls.Add(flow);

        // Summary stat cards
        var summaryRow = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(20, 20, 20) };
        var cardTotal  = MakeStatCard(out _bigTotal, Loc.T("StatsTotalLabel"));
        var cardWeek   = MakeStatCard(out _bigWeek,  Loc.T("StatsThisWeek"));
        var cardToday  = MakeStatCard(out _bigToday, Loc.T("StatsToday"));
        cardTotal.Location = new Point(24,  5);
        cardWeek.Location  = new Point(284, 5);
        cardToday.Location = new Point(544, 5);
        summaryRow.Controls.AddRange([cardTotal, cardWeek, cardToday]);

        // Chart
        var chartLbl = SectionLabel(Loc.T("StatsLast14Days"));
        chartLbl.Padding = new Padding(24, 0, 0, 0); chartLbl.TextAlign = ContentAlignment.MiddleLeft;
        _chartPanel = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.FromArgb(20, 20, 20) };
        _chartPanel.Paint += PaintDailyChart;

        // Bottom: artists (left) + tracks (center) + genres (right)
        var bottomSection = new Panel { Dock = DockStyle.Fill };

        var rightPanel = new Panel { Dock = DockStyle.Right, Width = 200, BackColor = Color.FromArgb(22, 22, 22) };
        var genreHdr   = SectionLabel("TOP GENRES");
        genreHdr.Padding = new Padding(16, 0, 0, 0); genreHdr.TextAlign = ContentAlignment.MiddleLeft;
        _genrePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _genrePanel.Paint += PaintGenreStats;
        rightPanel.Controls.Add(_genrePanel);
        rightPanel.Controls.Add(genreHdr);

        var divider2 = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Color.FromArgb(35, 35, 35) };

        var midPanel  = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 22, 22) };
        var tracksHdr = SectionLabel(Loc.T("StatsTopTracks"));
        tracksHdr.Padding = new Padding(16, 0, 0, 0); tracksHdr.TextAlign = ContentAlignment.MiddleLeft;
        _tracksPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _tracksPanel.Paint += PaintTopTracks;
        midPanel.Controls.Add(_tracksPanel);
        midPanel.Controls.Add(tracksHdr);

        var divider1 = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.FromArgb(35, 35, 35) };

        var leftPanel  = new Panel { Dock = DockStyle.Left, Width = 300, BackColor = Color.FromArgb(22, 22, 22) };
        var artistsHdr = SectionLabel(Loc.T("StatsTopArtists"));
        artistsHdr.Padding = new Padding(16, 0, 0, 0); artistsHdr.TextAlign = ContentAlignment.MiddleLeft;
        _artistsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _artistsPanel.Paint += PaintTopArtists;
        leftPanel.Controls.Add(_artistsPanel);
        leftPanel.Controls.Add(artistsHdr);

        bottomSection.Controls.Add(midPanel);
        bottomSection.Controls.Add(divider2);
        bottomSection.Controls.Add(rightPanel);
        bottomSection.Controls.Add(divider1);
        bottomSection.Controls.Add(leftPanel);

        _pageStats.Controls.Add(bottomSection);
        _pageStats.Controls.Add(Gap(4));
        _pageStats.Controls.Add(_chartPanel);
        _pageStats.Controls.Add(chartLbl);
        _pageStats.Controls.Add(summaryRow);
        _pageStats.Controls.Add(periodRow);
        _pageStats.Controls.Add(heading);
    }

    private void PeriodBtnClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        if (_activePeriodBtn is not null)
        {
            _activePeriodBtn.BackColor = Color.FromArgb(30, 30, 30);
            _activePeriodBtn.ForeColor = CDim;
        }
        btn.BackColor    = _cAccent;
        btn.ForeColor    = Color.White;
        _activePeriodBtn = btn;
        _statsPeriod     = btn.Tag?.ToString() ?? "local";
        LoadStatsPage();
    }

    private void LoadStatsPage()
    {
        if (_statsPeriod == "local" || !_engine.IsAuthenticated || string.IsNullOrEmpty(_settings.Username))
        {
            var (total, today, week) = _db.GetStats();
            _bigTotal.Text   = total.ToString("N0");
            _bigWeek.Text    = week.ToString("N0");
            _bigToday.Text   = today.ToString("N0");
            _dailyData       = _db.GetDailyScrobbles(14);
            _topArtistsData  = _db.GetTopArtists(10);
            _topTracksData   = _db.GetTopTracks(10);
            _genreData       = [];
            _statsSourceLbl.Text = "Source: local DB";
        }
        else
        {
            _statsSourceLbl.Text = "Loading…";
            _ = LoadStatsFromLastFmAsync(_statsPeriod);
            _dailyData = _db.GetDailyScrobbles(14);
        }

        _chartPanel.Invalidate();
        _artistsPanel.Invalidate();
        _tracksPanel.Invalidate();
        _genrePanel.Invalidate();
    }

    private async Task LoadStatsFromLastFmAsync(string period)
    {
        try
        {
            var username = _settings.Username!;
            var artists = await _engine.LastFmClient.GetUserTopArtistsAsync(username, period, 10);
            var tracks  = await _engine.LastFmClient.GetUserTopTracksAsync(username, period, 10);

            // Fetch genre tags for top 5 artists
            var genreAgg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (artist, _) in artists.Take(5))
            {
                var tags = await _engine.LastFmClient.GetArtistTopTagsAsync(artist);
                foreach (var tag in tags.Take(3))
                {
                    genreAgg.TryGetValue(tag, out var cur);
                    genreAgg[tag] = cur + 1;
                }
                await Task.Delay(150); // rate limit courtesy
            }

            if (InvokeRequired)
                Invoke(() => ApplyLastFmStats(artists, tracks, genreAgg, period));
            else
                ApplyLastFmStats(artists, tracks, genreAgg, period);
        }
        catch (Exception ex)
        {
            if (InvokeRequired) Invoke(() => _statsSourceLbl.Text = $"Error: {ex.Message}");
            else _statsSourceLbl.Text = $"Error: {ex.Message}";
        }
    }

    private void ApplyLastFmStats(
        (string name, int playcount)[] artists,
        (string artist, string name, int playcount)[] tracks,
        Dictionary<string, int> genreAgg,
        string period)
    {
        _topArtistsData = artists.Select(a => (a.name, a.playcount)).ToArray();
        _topTracksData  = tracks.Select(t => (t.artist, t.name, t.playcount)).ToArray();
        _genreData      = genreAgg.OrderByDescending(kv => kv.Value)
                                  .Take(8).Select(kv => (kv.Key, kv.Value)).ToArray();

        if (_topArtistsData.Length > 0)
        {
            _bigTotal.Text = _topArtistsData.Sum(a => a.count).ToString("N0");
            _bigWeek.Text  = "—";
            _bigToday.Text = "—";
        }
        _statsSourceLbl.Text = $"Last.fm · {period}";
        _artistsPanel.Invalidate();
        _tracksPanel.Invalidate();
        _genrePanel.Invalidate();
    }

    private void PaintDailyChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var r = _chartPanel.ClientRectangle;
        const int pl = 24, pr = 24, pt = 10, pb = 20;
        int left = pl, right = r.Width - pr, bottom = r.Height - pb;
        int chartW = right - left, chartH = bottom - pt;

        var today = DateTime.Today;
        var days  = new int[14];
        foreach (var (day, count) in _dailyData)
        {
            if (DateTime.TryParse(day, out var d))
            {
                int idx = (int)(today - d).TotalDays;
                if (idx >= 0 && idx < 14) days[13 - idx] = count;
            }
        }
        int maxCount = days.Max();
        if (maxCount == 0)
        {
            using var f = FontManager.Regular(9f);
            TextRenderer.DrawText(g, Loc.T("StatsNoData"), f, r, CDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        int slot = chartW / 14, barW = Math.Max(6, slot - 6);
        using var barBrush   = new SolidBrush(_cAccent);
        using var emptyBrush = new SolidBrush(Color.FromArgb(38, 38, 38));
        using var lblFont    = FontManager.Regular(7.5f);
        for (int i = 0; i < 14; i++)
        {
            int x = left + i * slot + (slot - barW) / 2;
            int barH = (int)((double)days[i] / maxCount * (chartH - 20));
            int y = bottom - 20 - barH;
            g.FillRectangle(days[i] > 0 ? barBrush : emptyBrush, x, days[i] > 0 ? y : bottom - 21, barW, days[i] > 0 ? barH : 1);
            if (i == 0 || i == 7 || i == 13)
                TextRenderer.DrawText(g, today.AddDays(i - 13).ToString("M/d"), lblFont,
                    new Rectangle(x - 8, bottom - 18, barW + 16, 16), CDim, TextFormatFlags.HorizontalCenter);
        }
    }

    private void PaintTopArtists(object? sender, PaintEventArgs e)
    {
        if (_topArtistsData.Length == 0)
        {
            using var ph = FontManager.Regular(9f);
            TextRenderer.DrawText(e.Graphics, "No data", ph, _artistsPanel.ClientRectangle, CDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var r    = _artistsPanel.ClientRectangle;
        int max  = Math.Max(1, _topArtistsData.Max(a => a.count));
        const int rowH = 36, px = 20, rankW = 24;
        int barAreaW = r.Width - px * 2 - rankW - 40;
        using var barBg       = new SolidBrush(Color.FromArgb(38, 38, 38));
        using var accentBrush = new SolidBrush(_cAccent);
        using var accentAlpha = new SolidBrush(Color.FromArgb(40, _cAccent));
        using var nameFont    = FontManager.Regular(9.5f);
        using var rankFont    = FontManager.Bold(8f);
        using var numFont     = FontManager.Regular(8.5f);
        for (int i = 0; i < Math.Min(10, _topArtistsData.Length); i++)
        {
            var (artist, count) = _topArtistsData[i];
            int y    = i * rowH + 2;
            int barW = (int)((double)count / max * barAreaW);
            int textX = px + rankW + 6;
            int textW = barAreaW - 4;

            // subtle row highlight on even rows
            if (i % 2 == 0) g.FillRectangle(accentAlpha, 0, y, r.Width, rowH - 2);

            // rank
            TextRenderer.DrawText(g, (i + 1).ToString(), rankFont,
                new Rectangle(px, y + 9, rankW, 18), Color.FromArgb(55, 55, 55), TextFormatFlags.Right);

            // artist name
            TextRenderer.DrawText(g, artist, nameFont,
                new Rectangle(textX, y + 6, textW, 18), CFg, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            // play count
            TextRenderer.DrawText(g, count.ToString("N0"), numFont,
                new Rectangle(r.Width - px - 38, y + 6, 38, 18), CDim, TextFormatFlags.Right);

            // bar
            g.FillRectangle(barBg,       textX, y + 27, barAreaW, 5);
            g.FillRectangle(accentBrush, textX, y + 27, barW,     5);
        }
    }

    private void PaintTopTracks(object? sender, PaintEventArgs e)
    {
        if (_topTracksData.Length == 0)
        {
            using var ph = FontManager.Regular(9f);
            TextRenderer.DrawText(e.Graphics, "No data", ph, _tracksPanel.ClientRectangle, CDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var r = _tracksPanel.ClientRectangle;
        const int rowH = 42, px = 16;
        using var titleFont = FontManager.Regular(9.5f);
        using var subFont   = FontManager.Regular(8.5f);
        using var sepBrush  = new SolidBrush(Color.FromArgb(36, 36, 36));
        for (int i = 0; i < Math.Min(10, _topTracksData.Length); i++)
        {
            var (artist, title, count) = _topTracksData[i];
            int y = i * rowH;
            if (i > 0) g.FillRectangle(sepBrush, px, y, r.Width - px * 2, 1);

            // rank badge
            var rankColor = i == 0 ? _cAccent : Color.FromArgb(44, 44, 44);
            using var rankBg = new SolidBrush(rankColor);
            g.FillRectangle(rankBg, px, y + 13, 22, 16);
            TextRenderer.DrawText(g, (i + 1).ToString(), FontManager.Bold(7.5f),
                new Rectangle(px, y + 13, 22, 16), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int textX = px + 30, textW = r.Width - textX - px - 46;
            TextRenderer.DrawText(g, title,  titleFont, new Rectangle(textX, y + 8,  textW, 17), CFg,  TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, artist, subFont,   new Rectangle(textX, y + 24, textW, 14), CDim, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, count.ToString("N0"), subFont,
                new Rectangle(r.Width - px - 42, y + 14, 42, 14), Color.FromArgb(90, 90, 90), TextFormatFlags.Right);
        }
    }

    private void PaintGenreStats(object? sender, PaintEventArgs e)
    {
        if (_genreData.Length == 0)
        {
            using var ph = FontManager.Regular(9f);
            TextRenderer.DrawText(e.Graphics, "Select a\nLast.fm period", ph,
                _genrePanel.ClientRectangle, CDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r   = _genrePanel.ClientRectangle;
        int max = Math.Max(1, _genreData.Max(x => x.count));
        const int rowH = 30, px = 14;
        int maxBarW = r.Width - px * 2;
        using var pillBg      = new SolidBrush(Color.FromArgb(36, 36, 36));
        using var accentBrush = new SolidBrush(_cAccent);
        using var font        = FontManager.Regular(9f);
        using var cntFont     = FontManager.Bold(7.5f);
        for (int i = 0; i < Math.Min(9, _genreData.Length); i++)
        {
            var (genre, count) = _genreData[i];
            int y    = i * rowH + 4;
            int barW = Math.Max(6, (int)((double)count / max * maxBarW));

            // pill background
            var pillRect = new Rectangle(px, y, barW, 22);
            g.FillRectangle(i == 0 ? accentBrush : pillBg, pillRect);

            // genre name
            var fgColor = i == 0 ? Color.White : CFg;
            TextRenderer.DrawText(g, genre, font,
                new Rectangle(px + 8, y + 3, barW - 10, 16), fgColor,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    private static Panel MakeStatCard(out Label bigNum, string label)
    {
        var card = new Panel { Size = new Size(240, 62), BackColor = Color.FromArgb(28, 28, 28) };
        bigNum = new Label { Location = new Point(16, 4), Size = new Size(208, 32), Text = "—", Font = FontManager.Bold(20f), ForeColor = CFg };
        var lbl = new Label { Location = new Point(16, 38), Size = new Size(208, 16), Text = label, Font = FontManager.Bold(7.5f), ForeColor = Color.FromArgb(70, 70, 70) };
        card.Controls.AddRange([bigNum, lbl]);
        return card;
    }

    // ── Friends Page ──────────────────────────────────────────────────────────

    private void BuildFriendsPage()
    {
        var heading = PageHeading("Friends");
        heading.Dock = DockStyle.Top;

        var topBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(20, 20, 20) };
        _friendsStatusLbl = new Label { Location = new Point(24, 12), Size = new Size(500, 20), Font = FontManager.Regular(9f), ForeColor = CDim };
        var refreshBtn = MakeBtn("Refresh", 100, 28);
        refreshBtn.Location = new Point(750, 8);
        refreshBtn.Click   += (_, _) => _ = RefreshFriendsAsync();
        topBar.Controls.AddRange([_friendsStatusLbl, refreshBtn]);

        _friendsListPanel = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = Color.FromArgb(22, 22, 22),
            Padding    = new Padding(0, 4, 0, 0),
        };

        _pageFriends.Controls.Add(_friendsListPanel);
        _pageFriends.Controls.Add(topBar);
        _pageFriends.Controls.Add(heading);
    }

    private async Task RefreshFriendsAsync()
    {
        if (!_engine.IsAuthenticated || string.IsNullOrEmpty(_settings.Username))
        {
            _friendsStatusLbl.Text = "Not logged in.";
            return;
        }
        _friendsStatusLbl.Text = "Loading friends…";
        _friendsListPanel.Controls.Clear();

        try
        {
            var friends = await _engine.LastFmClient.GetFriendsAsync(_settings.Username);
            if (InvokeRequired) Invoke(() => PopulateFriends(friends));
            else PopulateFriends(friends);
        }
        catch (Exception ex)
        {
            if (InvokeRequired) Invoke(() => _friendsStatusLbl.Text = $"Error: {ex.Message}");
            else _friendsStatusLbl.Text = $"Error: {ex.Message}";
        }
    }

    private void PopulateFriends((string name, string artist, string track, bool nowPlaying)[] friends)
    {
        _friendsListPanel.Controls.Clear();
        _friendsStatusLbl.Text = $"{friends.Length} friends";

        int y = 0;
        foreach (var (name, artist, track, nowPlaying) in friends)
        {
            var row = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(_friendsListPanel.ClientSize.Width, 50),
                BackColor = y % 100 < 50 ? Color.FromArgb(24, 24, 24) : Color.FromArgb(22, 22, 22),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            var nameLbl = new Label
            {
                Text = name, Location = new Point(20, 8), Size = new Size(200, 18),
                Font = FontManager.Bold(9.5f), ForeColor = CFg, AutoEllipsis = true,
            };
            var trackLbl = new Label
            {
                Text = track.Length > 0 ? $"{artist} — {track}" : "—",
                Location = new Point(20, 26), Size = new Size(600, 16),
                Font = FontManager.Regular(8.5f), ForeColor = CDim, AutoEllipsis = true,
            };
            var indicator = new Label
            {
                Text = nowPlaying ? "♪" : "",
                Location = new Point(230, 14), Size = new Size(20, 20),
                Font = FontManager.Regular(11f), ForeColor = _cAccent,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            row.Controls.AddRange([nameLbl, trackLbl, indicator]);
            _friendsListPanel.Controls.Add(row);
            y += 50;
        }

        if (friends.Length == 0)
        {
            var empty = new Label
            {
                Text = "No friends found. Add friends on Last.fm first.",
                Dock = DockStyle.Top, Height = 40,
                Font = FontManager.Regular(9f), ForeColor = CDim,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _friendsListPanel.Controls.Add(empty);
        }
    }

    // ── Monitor Events ────────────────────────────────────────────────────────

    private void OnNowPlaying(object? sender, Track? track)
    {
        if (InvokeRequired) { Invoke(() => OnNowPlaying(sender, track)); return; }

        bool isNewTrack = track is null || _currentTrack is null || !track.IsSameTrack(_currentTrack);
        _currentTrack = track;

        if (track is null)
        {
            _scrobbled = false; _monTitle.Text = "—"; _monArtist.Text = ""; _monAlbum.Text = "";
            SetStatus(Loc.T("NotPlaying"), CDim);
            _monBar.Value = 0; _monEta.Text = "";
            SetLoveBtn(enabled: false, loved: false);
            _albumArt.Image = null;
            _artistInfoPanel.Visible = false;
            return;
        }

        _monAlbum.Text = string.IsNullOrEmpty(track.Album) ? Loc.T("ResolvingAlbum") : track.Album;
        if (!isNewTrack) return;

        _scrobbled = false; _startedAt = track.DetectedAt; _threshMs = _engine.GetScrobbleThresholdMs(track);
        _monTitle.Text = track.Title; _monArtist.Text = track.Artist;
        SetStatus(Loc.T("WaitingToScrobble"), Color.FromArgb(200, 160, 0));
        AppendLog(LogKind.NowPlaying, $"{track.Artist} — {track.Title}");
        SetLoveBtn(enabled: _engine.IsAuthenticated, loved: false);
        _trackLoved = false;

        _albumArt.Image = null; _artCts?.Cancel(); _artCts = new CancellationTokenSource();
        _ = LoadAlbumArtAsync(_artCts.Token);

        _bioCts?.Cancel(); _bioCts = new CancellationTokenSource();
        _ = LoadArtistBioAsync(track.Artist, _bioCts.Token);
    }

    private async Task LoadAlbumArtAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(1200, ct);
            var img = await _engine.GetCurrentThumbnailAsync();
            if (img is null || ct.IsCancellationRequested) return;
            if (InvokeRequired) Invoke(() => { _albumArt.Image?.Dispose(); _albumArt.Image = img; });
            else { _albumArt.Image?.Dispose(); _albumArt.Image = img; }
        }
        catch (OperationCanceledException) { }
    }

    private async Task LoadArtistBioAsync(string artist, CancellationToken ct)
    {
        if (artist == _lastBioArtist) return;
        try
        {
            await Task.Delay(800, ct);
            if (ct.IsCancellationRequested) return;
            var (bio, similar) = await _engine.LastFmClient.GetArtistInfoAsync(artist);
            if (ct.IsCancellationRequested) return;
            _lastBioArtist = artist;
            if (InvokeRequired) Invoke(() => UpdateArtistBio(bio, similar));
            else UpdateArtistBio(bio, similar);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void UpdateArtistBio(string bio, string[] similar)
    {
        _artistBioText.Text = bio.Length > 0 ? bio : "(No bio available)";
        _similarFlow.Controls.Clear();
        foreach (var name in similar)
        {
            var chip = new Label
            {
                Text      = name,
                AutoSize  = false,
                Size      = new Size(Math.Min(248, TextRenderer.MeasureText(name, FontManager.Regular(8f)).Width + 16), 22),
                Font      = FontManager.Regular(8f),
                ForeColor = CDim,
                BackColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0, 0, 4, 4),
                AutoEllipsis = true,
            };
            chip.Click += (_, _) => OpenUrl($"https://www.last.fm/music/{Uri.EscapeDataString(name)}");
            _similarFlow.Controls.Add(chip);
        }
        _artistInfoPanel.Visible = bio.Length > 0 || similar.Length > 0;
    }

    private void OnScrobbled(object? sender, (Track track, bool success) e)
    {
        if (InvokeRequired) { Invoke(() => OnScrobbled(sender, e)); return; }
        _scrobbled = true;
        if (e.success)
        {
            SetStatus(Loc.T("ScrobbledOk"), Color.FromArgb(80, 200, 80));
            _monBar.Value = 100;
            AppendLog(LogKind.Scrobbled, $"{e.track.Artist} — {e.track.Title}");
        }
        else
        {
            SetStatus(Loc.T("ScrobbleFailed"), Color.FromArgb(220, 60, 60));
            AppendLog(LogKind.Failed, $"{e.track.Artist} — {e.track.Title}");
        }
        RefreshMonitorStats();
    }

    private void OnQueueFlushed(object? sender, int count)
    {
        if (InvokeRequired) { Invoke(() => OnQueueFlushed(sender, count)); return; }
        AppendLog(LogKind.QueueFlushed, string.Format(Loc.T("QueueFlushed"), count));
        RefreshMonitorStats();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_currentTrack is null || _scrobbled || _threshMs <= 0) return;
        var elapsed   = (DateTime.UtcNow - _startedAt).TotalMilliseconds;
        _monBar.Value = (int)Math.Min(100, elapsed / _threshMs * 100);
        var rem       = TimeSpan.FromMilliseconds(Math.Max(0, _threshMs - elapsed));
        _monEta.Text  = rem.TotalSeconds < 1 ? Loc.T("Now") : $"–{(int)rem.TotalSeconds}s";
    }

    private void SetStatus(string t, Color c) { _monStatus.Text = t; _monStatus.ForeColor = c; }

    private void SetLoveBtn(bool enabled, bool loved)
    {
        _loveBtn.Enabled   = enabled;
        _loveBtn.Text      = loved ? "♥" : "♡";
        _loveBtn.ForeColor = loved ? Color.FromArgb(220, 60, 80) : Color.FromArgb(100, 100, 100);
    }

    private async void LoveBtnClicked(object? sender, EventArgs e)
    {
        if (_currentTrack is null) return;
        _loveBtn.Enabled = false;
        _trackLoved      = !_trackLoved;
        SetLoveBtn(enabled: false, loved: _trackLoved);
        await _engine.LoveTrackAsync(_currentTrack, _trackLoved);
        AppendLog(_trackLoved ? LogKind.Loved : LogKind.Unloved,
                  $"{_currentTrack.Artist} — {_currentTrack.Title}");
        _loveBtn.Enabled = _engine.IsAuthenticated;
    }

    private void AppendLog(LogKind kind, string text) => _monLog.AddEntry(kind, text);

    // ── History Events ────────────────────────────────────────────────────────

    private async void ManualScrobbleClicked(object? sender, EventArgs e)
    {
        using var form = new ManualScrobbleForm(_currentTrack?.Artist, _currentTrack?.Title, _currentTrack?.Album);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        bool ok = await _engine.ManualScrobbleAsync(form.Artist, form.TrackTitle, form.Album, form.PlayedAt);
        LoadHistory(); RefreshStats();
        AppendLog(ok ? LogKind.Manual : LogKind.Failed,
                  $"{form.Artist} — {form.TrackTitle}");
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        _threshPct.Value   = Math.Clamp(_settings.ScrobbleThresholdPercent,    10, 100);
        _threshMax.Value   = Math.Clamp(_settings.ScrobbleThresholdMaxSeconds, 30, 600);
        _dupWindow.Value   = Math.Clamp(_settings.DuplicateWindowMinutes,       0,  60);
        _goalSpin.Value    = Math.Clamp(_settings.DailyScrobbleGoal,            0, 999);
        _filterAppleChk.Checked = _settings.FilterAppleMusicOnly;
        _editBeforeChk.Checked  = _settings.EditBeforeScrobble;
        _showNotifChk.Checked   = _settings.ShowNowPlayingNotification;
        _startWinChk.Checked    = _settings.StartWithWindows;
        _autoNormChk.Checked    = _settings.AutoNormalize;
        _webhookUrlBox.Text          = _settings.WebhookUrl;
        _webhookScrobbleChk.Checked   = _settings.WebhookOnScrobble;
        _webhookNowPlayingChk.Checked = _settings.WebhookOnNowPlaying;
        var langIdx = Loc.Available.ToList().FindIndex(l => l.Code == _settings.Language);
        _langCombo.SelectedIndex = langIdx >= 0 ? langIdx : 0;
        UpdateAuthStatus();
    }

    private void UpdateAuthStatus()
    {
        if (!string.IsNullOrEmpty(_settings.SessionKey))
        {
            _authStatusLabel.Text      = string.Format(Loc.T("LoggedInAs"), _settings.Username ?? "?");
            _authStatusLabel.ForeColor = Color.FromArgb(80, 200, 80);
            _authBtn.Text              = Loc.T("LogInAgain");
            _profileLink.Text          = $"last.fm/user/{_settings.Username} →";
            _profileLink.Visible       = true;
            _profileLink.Links.Clear();
            _profileLink.Links.Add(0, _profileLink.Text.Length);
        }
        else
        {
            _authStatusLabel.Text      = Loc.T("NotLoggedIn");
            _authStatusLabel.ForeColor = Color.FromArgb(220, 80, 80);
            _authBtn.Text              = Loc.T("LoginWithLastFm");
            _profileLink.Visible       = false;
        }
    }

    private void LoadRules()
    {
        _rulesGrid.Rows.Clear();
        foreach (var rule in _db.LoadRules())
        {
            var i = _rulesGrid.Rows.Add(rule.IsEnabled, rule.Field.ToString(), rule.Description, rule.Pattern, rule.Replacement);
            _rulesGrid.Rows[i].Tag = rule;
            if (rule.IsBuiltIn) _rulesGrid.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(70, 70, 70);
        }
    }


    private void SaveScrobbleSettings()
    {
        _settings.ScrobbleThresholdPercent    = (int)_threshPct.Value;
        _settings.ScrobbleThresholdMaxSeconds = (int)_threshMax.Value;
        _settings.DuplicateWindowMinutes      = (int)_dupWindow.Value;
        _settings.FilterAppleMusicOnly        = _filterAppleChk.Checked;
        _settings.EditBeforeScrobble          = _editBeforeChk.Checked;
        _settings.ShowNowPlayingNotification  = _showNotifChk.Checked;
        _settings.StartWithWindows            = _startWinChk.Checked;
        _settings.DailyScrobbleGoal           = (int)_goalSpin.Value;
        _settings.WebhookUrl                  = _webhookUrlBox.Text.Trim();
        _settings.WebhookOnScrobble           = _webhookScrobbleChk.Checked;
        _settings.WebhookOnNowPlaying         = _webhookNowPlayingChk.Checked;
        if (_langCombo.SelectedIndex >= 0)
        {
            var newLang = Loc.Available[_langCombo.SelectedIndex].Code;
            if (newLang != _settings.Language)
            {
                _settings.Language = newLang;
                MessageBox.Show(Loc.T("RestartToApplyLang"), Loc.T("RestartRequired"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        _db.SaveSettings(_settings);
        _engine.UpdateSettings(_settings);
        ApplyStartWithWindows(_settings.StartWithWindows);
        RefreshMonitorStats();
    }

    private void SaveNormSettings()
    {
        foreach (DataGridViewRow row in _rulesGrid.Rows)
        {
            if (row.Tag is not NormalizationRule rule) continue;
            rule.IsEnabled   = Convert.ToBoolean(row.Cells["Enabled"].Value);
            rule.Pattern     = row.Cells["Pattern"].Value?.ToString() ?? rule.Pattern;
            rule.Replacement = row.Cells["Replace"].Value?.ToString() ?? rule.Replacement;
            _db.SaveRule(rule);
        }
        _settings.AutoNormalize = _autoNormChk.Checked;
        _db.SaveSettings(_settings);
        _engine.UpdateSettings(_settings);
        _engine.ReloadRules();
    }

    private void AuthClicked(object? sender, EventArgs e)
    {
        using var af = new AuthForm(_engine.LastFmClient);
        if (af.ShowDialog(this) == DialogResult.OK)
        {
            _settings.SessionKey = af.SessionKey;
            _settings.Username   = af.Username;
            _db.SaveSettings(_settings);
            _engine.UpdateSettings(_settings);
            UpdateAuthStatus();
        }
    }

    private void AddRuleClicked(object? sender, EventArgs e)
    {
        _db.SaveRule(new NormalizationRule { Field = RuleField.Title, Pattern = @"\s*\(example\)", Replacement = "", Description = Loc.T("NewRule"), IsEnabled = true });
        LoadRules();
    }

    private void DeleteRuleClicked(object? sender, EventArgs e)
    {
        foreach (DataGridViewRow row in _rulesGrid.SelectedRows)
            if (row.Tag is NormalizationRule rule && !rule.IsBuiltIn)
                _db.DeleteRule(rule.Id);
        LoadRules();
    }

    // ── Accent Color ──────────────────────────────────────────────────────────

    private void PickAccentColorClicked(object? sender, EventArgs e)
    {
        using var dlg = new ColorDialog { Color = _cAccent, FullOpen = true, AllowFullOpen = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        UpdateAccentColor(dlg.Color);
        _settings.AccentColor = ColorTranslator.ToHtml(dlg.Color);
        _db.SaveSettings(_settings);
    }

    private void UpdateAccentColor(Color c)
    {
        _cAccent = c;
        _accentBar.BackColor = c;
        _monBar.ForeColor    = c;
        foreach (var btn in _accentBtns) btn.BackColor = c;
        if (_activeNavBtn is not null) _activeNavBtn.BackColor = c;
        if (_monLog       is not null) { _monLog.Accent = c; _monLog.Invalidate(); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NavButton NavBtn(string text, string icon) => new(icon, text);

    private static Label PageHeading(string text) => new()
    {
        Text = text, Height = 54, Font = FontManager.Bold(14f), ForeColor = CFg,
        Padding = new Padding(26, 0, 0, 10), TextAlign = ContentAlignment.BottomLeft,
    };

    private static Label SectionLabel(string text) => new()
    {
        Text = text, Dock = DockStyle.Top, Height = 22, Font = FontManager.Bold(7.5f),
        ForeColor = Color.FromArgb(65, 65, 65), TextAlign = ContentAlignment.BottomLeft,
    };

    private static Label StatLabel(string text) => new()
    {
        Text = text, AutoSize = true, Font = FontManager.Regular(9f), ForeColor = CDim,
    };

    private static Label Gap(int h) => new() { Dock = DockStyle.Top, Height = h };

    private static TextBox MakeInput(int w, int h) => new()
    {
        Size = new Size(w, h), BackColor = CInput, ForeColor = CFg,
        BorderStyle = BorderStyle.FixedSingle, Font = FontManager.Regular(9.5f),
    };

    private static Button MakeBtn(string text, int w, int h) => new()
    {
        Text = text, Size = new Size(w, h), FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(38, 38, 38), ForeColor = CFg, Cursor = Cursors.Hand,
        Font = FontManager.Regular(9.5f), UseVisualStyleBackColor = false,
    };

    private static CheckBox MakeChk(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = CFg, Font = FontManager.Regular(9.5f),
    };

    private static Label RowLabel(string text, int x, int y) => new()
    {
        Text = text, Location = new Point(x, y), Size = new Size(138, 20),
        ForeColor = CDim, Font = FontManager.Regular(9.5f),
    };

    private static Color ColorFromHex(string? hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
    }

    private static void ApplyStartWithWindows(bool enable)
    {
        try
        {
            var exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exe is null) return;
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;
            if (enable) key.SetValue("LastFmScrobbler", $"\"{exe}\"");
            else        key.DeleteValue("LastFmScrobbler", throwOnMissingValue: false);
        }
        catch { }
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    // ── Custom title bar ──────────────────────────────────────────────────────

    private Panel BuildTitleBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(10, 10, 10) };

        var logoPane = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = CSidebar };
        var logoLbl  = new Label
        {
            Text = "last.fm Scrobbler", Dock = DockStyle.Fill, ForeColor = _cAccent,
            Font = FontManager.Bold(11.5f), TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0), BackColor = Color.Transparent,
        };
        logoPane.Controls.Add(logoLbl);

        var rightPane = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(16, 16, 16) };

        var closeBtn = TitleBtn("✕", 46);
        closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
        closeBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(150, 25, 10);
        closeBtn.MouseEnter += (_, _) => closeBtn.ForeColor = Color.White;
        closeBtn.MouseLeave += (_, _) => closeBtn.ForeColor = Color.FromArgb(150, 150, 150);
        closeBtn.Click += (_, _) => Close();

        _maxBtn = TitleBtn("□", 40);
        _maxBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 38);
        _maxBtn.Click += (_, _) =>
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else { MaximizedBounds = Screen.GetWorkingArea(this); WindowState = FormWindowState.Maximized; }
        };

        var minBtn = TitleBtn("─", 40);
        minBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 38);
        minBtn.Click += (_, _) => WindowState = FormWindowState.Minimized;

        rightPane.Controls.Add(minBtn);
        rightPane.Controls.Add(_maxBtn);
        rightPane.Controls.Add(closeBtn);

        MouseEventHandler drag = (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        };
        bar.MouseDown += drag; logoPane.MouseDown += drag; logoLbl.MouseDown += drag; rightPane.MouseDown += drag;
        bar.DoubleClick += (_, _) => _maxBtn.PerformClick();
        rightPane.DoubleClick += (_, _) => _maxBtn.PerformClick();

        bar.Controls.Add(rightPane);
        bar.Controls.Add(logoPane);
        return bar;
    }

    private static Button TitleBtn(string symbol, int width) => new()
    {
        Text = symbol, Size = new Size(width, 48), Dock = DockStyle.Right,
        FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(150, 150, 150),
        BackColor = Color.Transparent, Font = FontManager.Regular(10f),
        UseVisualStyleBackColor = false, Cursor = Cursors.Default,
        FlatAppearance = { BorderSize = 0, MouseDownBackColor = Color.FromArgb(32, 32, 32) },
    };

    // ── Resize + shadow ───────────────────────────────────────────────────────

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST       = 0x84;
        const int WM_QUERYENDSESSION = 0x11;
        const int WM_ENDSESSION      = 0x16;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int grip = 6;

        // Windows / Restart Manager / Inno Setup signal app to close.
        if (m.Msg == WM_QUERYENDSESSION || m.Msg == WM_ENDSESSION)
        {
            _externalCloseRequested = true;
            if (m.Msg == WM_QUERYENDSESSION) m.Result = (IntPtr)1;
            BeginInvoke(() => Application.Exit());
        }

        if (m.Msg == WM_NCHITTEST)
        {
            var p = PointToClient(Cursor.Position);
            bool l = p.X < grip, r = p.X >= ClientSize.Width - grip;
            bool t = p.Y < grip, b = p.Y >= ClientSize.Height - grip;
            if (t && l) { m.Result = (IntPtr)HTTOPLEFT;     return; }
            if (t && r) { m.Result = (IntPtr)HTTOPRIGHT;    return; }
            if (b && l) { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
            if (b && r) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
            if (l)      { m.Result = (IntPtr)HTLEFT;        return; }
            if (r)      { m.Result = (IntPtr)HTRIGHT;       return; }
            if (b)      { m.Result = (IntPtr)HTBOTTOM;      return; }
        }
        base.WndProc(ref m);
    }

    // ── NavButton ─────────────────────────────────────────────────────────────

    private sealed class NavButton : Button
    {
        private static readonly Color CHover = Color.FromArgb(28, 28, 28);
        private static readonly Color CBg    = Color.FromArgb(15, 15, 15);
        private readonly string _icon;
        private bool _hovered;

        public NavButton(string icon, string label)
        {
            _icon = icon; Text = label; Dock = DockStyle.Top; Height = 46;
            FlatStyle = FlatStyle.Flat; ForeColor = CDim; BackColor = Color.Transparent;
            Font = FontManager.Regular(10f); Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false; FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool isActive = BackColor != Color.Transparent && BackColor != CBg;
            e.Graphics.Clear(isActive ? Color.FromArgb(26, 26, 26) : (_hovered ? CHover : CBg));
            if (isActive) { using var bar = new SolidBrush(BackColor); e.Graphics.FillRectangle(bar, 0, 0, 3, Height); }
            var tf = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(e.Graphics, _icon, Font, new Rectangle(16, 0, 24, Height), ForeColor, tf | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics, Text,  Font, new Rectangle(50, 0, Width - 56, Height), ForeColor, tf | TextFormatFlags.Left);
        }
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _engine.NowPlayingChanged   -= OnNowPlaying;
        _engine.TrackScrobbled      -= OnScrobbled;
        _engine.PendingQueueFlushed -= OnQueueFlushed;
        _tick.Stop(); _tick.Dispose();
        base.OnFormClosed(e);
    }
}
