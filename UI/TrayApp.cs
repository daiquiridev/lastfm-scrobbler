using LastFmScrobbler.Core;
using LastFmScrobbler.Data;
using LastFmScrobbler.Models;

namespace LastFmScrobbler.UI;

public class TrayApp : ApplicationContext
{
    private readonly Database        _db;
    private readonly ScrobbleEngine  _engine;
    private readonly AppSettings     _settings;
    private readonly NotifyIcon      _tray;
    private readonly MainForm        _mainForm;
    private readonly HotkeyManager   _hotkeys;
    private readonly UpdateChecker   _updater = new();
    private readonly ToolStripMenuItem _nowPlayingItem;
    private readonly ToolStripMenuItem _scrobbleCountItem;
    private readonly ToolStripMenuItem _copyNowPlayingItem;
    private readonly ToolStripMenuItem _updateItem;
    private int    _sessionScrobbles;
    private Track? _currentTrack;

    public TrayApp(Database db, ScrobbleEngine engine, AppSettings settings)
    {
        _db       = db;
        _engine   = engine;
        _settings = settings;

        _mainForm = new MainForm(_db, _engine, _settings);
        _ = _mainForm.Handle;

        _nowPlayingItem     = new ToolStripMenuItem("Not playing") { Enabled = false };
        _scrobbleCountItem  = new ToolStripMenuItem("0 scrobbles this session") { Enabled = false };
        _copyNowPlayingItem = new ToolStripMenuItem("Copy Now Playing\tCtrl+Alt+C");
        _copyNowPlayingItem.Click += (_, _) => CopyNowPlaying();

        _updateItem = new ToolStripMenuItem("Check for Updates");
        _updateItem.Click += OnUpdateItemClicked;

        var menu = new ContextMenuStrip();
        menu.Items.Add(_nowPlayingItem);
        menu.Items.Add(_scrobbleCountItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_copyNowPlayingItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Monitor",  null, (_, _) => _mainForm.ShowMonitor());
        menu.Items.Add("Settings", null, (_, _) => _mainForm.ShowSettings());
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        var appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        _tray = new NotifyIcon
        {
            Icon             = appIcon,
            Text             = "Last.fm Scrobbler",
            Visible          = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => _mainForm.ShowMonitor();

        _engine.ConfirmBeforeScrobble = async track =>
        {
            bool result = false;
            await Task.Run(() => _mainForm.Invoke(() =>
            {
                using var form = new EditTrackForm(track);
                result = form.ShowDialog() == DialogResult.OK;
            }));
            return result;
        };

        _engine.NowPlayingChanged += OnNowPlayingChanged;
        _engine.TrackScrobbled   += OnTrackScrobbled;

        // Global hotkeys: Ctrl+Alt+L = Love, Ctrl+Alt+C = Copy
        _hotkeys = new HotkeyManager();
        _hotkeys.Register(Keys.L, () => _mainForm.Invoke(() => _mainForm.ToggleLoveCurrentTrack()));
        _hotkeys.Register(Keys.C, () => _mainForm.Invoke(() => CopyNowPlaying()));

        // First-run users without a session need to see the login screen — show
        // the form. Returning users start in tray and can open via double-click
        // or the tray menu.
        if (string.IsNullOrEmpty(_settings.SessionKey))
            _mainForm.ShowMonitor();

        _ = StartEngineAsync();
        _ = CheckForUpdateAsync(onStartup: true);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private UpdateChecker.UpdateInfo? _pendingUpdate;
    private bool _updateDialogOpen;

    private async Task CheckForUpdateAsync(bool onStartup = false)
    {
        if (onStartup) await Task.Delay(TimeSpan.FromSeconds(5));

        UpdateChecker.UpdateInfo? info;
        try { info = await _updater.CheckAsync(); }
        catch { info = null; }

        void Apply()
        {
            _updateItem.Enabled = true;
            _pendingUpdate      = info;
            _updateItem.Text    = info is null ? "Check for Updates" : $"Install Update v{info.Version}…";

            // Always show dialog when an update is found — balloon tips can be
            // suppressed by Windows Focus Assist or notification settings, but
            // a dialog is always visible to the user.
            if (info is not null && !_updateDialogOpen)
                ShowUpdateDialog(info);
        }

        if (_tray.ContextMenuStrip!.InvokeRequired) _tray.ContextMenuStrip.Invoke(Apply);
        else Apply();
    }

    private void OnUpdateItemClicked(object? sender, EventArgs e)
    {
        ShowUpdateDialog(_pendingUpdate);
    }

    private void ShowUpdateDialog(UpdateChecker.UpdateInfo? preFound)
    {
        if (_updateDialogOpen) return;

        var accent = ParseAccent(_settings.AccentColor);
        var dlg    = new UpdateDialog(_updater, accent, preFound);
        _updateDialogOpen = true;
        dlg.FormClosed += (_, _) => _updateDialogOpen = false;
        dlg.Show();
        dlg.BringToFront();
        dlg.Activate();
    }

    private static Color ParseAccent(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.FromArgb(186, 0, 0);
        try { return ColorTranslator.FromHtml(hex); } catch { return Color.FromArgb(186, 0, 0); }
    }

    // ── Engine & tray ─────────────────────────────────────────────────────────

    private void CopyNowPlaying()
    {
        if (_currentTrack is null) return;
        var text = string.IsNullOrEmpty(_currentTrack.Album)
            ? $"{_currentTrack.Artist} — {_currentTrack.Title}"
            : $"{_currentTrack.Artist} — {_currentTrack.Title} ({_currentTrack.Album})";
        Clipboard.SetText(text);
        _tray.ShowBalloonTip(1500, "Copied", text, ToolTipIcon.None);
    }

    private async Task StartEngineAsync()
    {
        try
        {
            await _engine.StartAsync();
            if (!_engine.IsAuthenticated)
                _tray.ShowBalloonTip(4000, "Last.fm Scrobbler",
                    "Not authenticated. Right-click → Settings to log in.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(5000, "Startup error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void OnNowPlayingChanged(object? sender, Track? track)
    {
        if (_tray.ContextMenuStrip!.InvokeRequired)
        { _tray.ContextMenuStrip.Invoke(() => OnNowPlayingChanged(sender, track)); return; }

        _currentTrack = track;

        if (track is null)
        {
            _nowPlayingItem.Text        = "Not playing";
            _copyNowPlayingItem.Enabled = false;
            _tray.Text                  = "Last.fm Scrobbler";
        }
        else
        {
            var display = $"{track.Artist} – {track.Title}";
            _nowPlayingItem.Text        = Truncate(display, 60);
            _copyNowPlayingItem.Enabled = true;
            _tray.Text                  = Truncate($"♪ {display}", 63);

            if (_settings.ShowNowPlayingNotification)
                _tray.ShowBalloonTip(2000, "Now Playing", display, ToolTipIcon.None);
        }
    }

    private void OnTrackScrobbled(object? sender, (Track track, bool success) e)
    {
        if (_tray.ContextMenuStrip!.InvokeRequired)
        { _tray.ContextMenuStrip.Invoke(() => OnTrackScrobbled(sender, e)); return; }

        if (e.success)
        {
            _sessionScrobbles++;
            _scrobbleCountItem.Text = $"{_sessionScrobbles} scrobble{(_sessionScrobbles == 1 ? "" : "s")} this session";
        }
    }

    private void ExitApp()
    {
        _hotkeys.Dispose();
        _tray.Visible = false;
        _mainForm.Dispose();
        _engine.Dispose();
        _db.Dispose();
        Application.Exit();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
