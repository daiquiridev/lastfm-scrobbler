using System.Diagnostics;
using LastFmScrobbler.Core;

namespace LastFmScrobbler.UI;

public class AuthForm : Form
{
    private readonly LastFmClient _client;
    public string? SessionKey { get; private set; }
    public string? Username   { get; private set; }

    private string? _pendingToken;
    private Label   _statusLabel    = null!;
    private Button  _openBrowserBtn = null!;
    private System.Windows.Forms.Timer _pollTimer = null!;
    private int _pollAttempts;
    private const int MaxPollAttempts = 90; // 3 minutes

    public AuthForm(LastFmClient client)
    {
        _client = client;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text            = "Last.fm Authentication";
        Size            = new Size(440, 200);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = Color.FromArgb(28, 28, 28);
        ForeColor       = Color.FromArgb(220, 220, 220);
        Font            = FontManager.Regular(9.5f);

        var lbl = new Label
        {
            Text      = "Click 'Open Browser' to authorize this app on Last.fm.\nThe app will detect authorization automatically.",
            Location  = new Point(18, 18),
            Size      = new Size(396, 50),
            ForeColor = Color.FromArgb(180, 180, 180),
        };

        _statusLabel = new Label
        {
            Text      = "Ready.",
            Location  = new Point(18, 76),
            Size      = new Size(396, 24),
            ForeColor = Color.FromArgb(110, 110, 110),
            Font      = FontManager.Regular(9f),
        };

        _openBrowserBtn = MakeBtn("Open Browser", 150, 34);
        _openBrowserBtn.Location = new Point(18, 110);
        _openBrowserBtn.Click   += OpenBrowserClicked;

        var cancelBtn = MakeBtn("Cancel", 90, 34);
        cancelBtn.Location     = new Point(178, 110);
        cancelBtn.DialogResult = DialogResult.Cancel;

        _pollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _pollTimer.Tick += PollTick;

        Controls.AddRange([lbl, _statusLabel, _openBrowserBtn, cancelBtn]);
    }

    private static Button MakeBtn(string text, int w, int h) => new()
    {
        Text                    = text,
        Size                    = new Size(w, h),
        FlatStyle               = FlatStyle.Flat,
        BackColor               = Color.FromArgb(40, 40, 40),
        ForeColor               = Color.FromArgb(220, 220, 220),
        Cursor                  = Cursors.Hand,
        UseVisualStyleBackColor = false,
        FlatAppearance          = { BorderSize = 0 },
    };

    private async void OpenBrowserClicked(object? sender, EventArgs e)
    {
        try
        {
            _openBrowserBtn.Enabled = false;
            _statusLabel.Text       = "Fetching token...";
            _statusLabel.ForeColor  = Color.FromArgb(200, 160, 0);

            var (token, url) = await _client.GetAuthUrlAsync();
            _pendingToken = token;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            _statusLabel.Text = "Waiting for authorization in browser...";
            _pollAttempts     = 0;
            _pollTimer.Start();
        }
        catch (Exception ex)
        {
            _statusLabel.Text       = $"Error: {ex.Message}";
            _statusLabel.ForeColor  = Color.FromArgb(220, 60, 60);
            _openBrowserBtn.Enabled = true;
        }
    }

    private async void PollTick(object? sender, EventArgs e)
    {
        _pollAttempts++;
        if (_pollAttempts > MaxPollAttempts || _pendingToken is null)
        {
            _pollTimer.Stop();
            _statusLabel.Text       = "Timed out. Click 'Open Browser' to try again.";
            _statusLabel.ForeColor  = Color.FromArgb(220, 60, 60);
            _openBrowserBtn.Enabled = true;
            return;
        }

        try
        {
            var (sk, name) = await _client.GetSessionAsync(_pendingToken);
            _pollTimer.Stop();
            SessionKey             = sk;
            Username               = name;
            _statusLabel.Text      = $"Authenticated as {name}!";
            _statusLabel.ForeColor = Color.FromArgb(80, 200, 80);
            DialogResult           = DialogResult.OK;
            Close();
        }
        catch
        {
            var remaining = MaxPollAttempts - _pollAttempts;
            _statusLabel.Text      = $"Waiting... ({remaining * 2}s remaining)";
            _statusLabel.ForeColor = Color.FromArgb(110, 110, 110);
        }
    }
}
