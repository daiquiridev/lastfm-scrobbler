using LastFmScrobbler.Localization;

namespace LastFmScrobbler.UI;

public class ManualScrobbleForm : Form
{
    public string TrackTitle { get; private set; } = string.Empty;
    public string Artist     { get; private set; } = string.Empty;
    public string Album      { get; private set; } = string.Empty;
    public DateTime PlayedAt { get; private set; }

    private TextBox      _titleBox   = null!;
    private TextBox      _artistBox  = null!;
    private TextBox      _albumBox   = null!;
    private DateTimePicker _datePicker = null!;

    public ManualScrobbleForm(string? artist = null, string? title = null, string? album = null)
    {
        InitializeComponent();
        if (artist != null) _artistBox.Text = artist;
        if (title  != null) _titleBox.Text  = title;
        if (album  != null) _albumBox.Text  = album;
    }

    private void InitializeComponent()
    {
        Text            = Loc.T("ManualScrobbleTitle");
        Size            = new Size(460, 272);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = FontManager.Regular(9.5f);
        BackColor       = Color.FromArgb(28, 28, 28);
        ForeColor       = Color.FromArgb(220, 220, 220);

        const int lx = 14, rx = 120, rw = 300, y0 = 18;

        _titleBox  = Input(rw, 26);
        _artistBox = Input(rw, 26);
        _albumBox  = Input(rw, 26);

        _datePicker = new DateTimePicker
        {
            Location        = new Point(rx, y0 + 3 * 38),
            Size            = new Size(rw, 26),
            Format          = DateTimePickerFormat.Custom,
            CustomFormat    = "yyyy-MM-dd  HH:mm",
            ShowUpDown      = false,
            Value           = DateTime.Now,
            CalendarForeColor       = Color.FromArgb(220, 220, 220),
            CalendarMonthBackground = Color.FromArgb(36, 36, 36),
            Font            = FontManager.Regular(9.5f),
        };

        void Row(string label, Control ctrl, int row)
        {
            Controls.Add(new Label
            {
                Text      = label,
                Location  = new Point(lx, y0 + row * 38 + 5),
                Size      = new Size(100, 20),
                ForeColor = Color.FromArgb(100, 100, 100),
                Font      = FontManager.Regular(9f),
            });
            ctrl.Location = new Point(rx, y0 + row * 38);
            Controls.Add(ctrl);
        }

        Row(Loc.T("LblTrackName"), _titleBox,   0);
        Row(Loc.T("LblArtist"),   _artistBox,  1);
        Row(Loc.T("LblAlbum"),    _albumBox,   2);
        Row(Loc.T("LblPlayedAt"), _datePicker, 3);

        int by = y0 + 4 * 38 + 6;

        var scrobbleBtn = MakeBtn("Scrobble", 110, 32);
        scrobbleBtn.Location  = new Point(rx, by);
        scrobbleBtn.BackColor = Color.FromArgb(186, 0, 0);
        scrobbleBtn.ForeColor = Color.White;
        scrobbleBtn.Click    += (_, _) =>
        {
            var t = _titleBox.Text.Trim();
            var a = _artistBox.Text.Trim();
            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(a))
            {
                MessageBox.Show(Loc.T("MsgTrackArtistRequired"), Loc.T("TitleMissingInfo"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            TrackTitle   = t;
            Artist       = a;
            Album        = _albumBox.Text.Trim();
            PlayedAt     = _datePicker.Value;
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelBtn = MakeBtn(Loc.T("Cancel"), 90, 32);
        cancelBtn.Location    = new Point(rx + 120, by);
        cancelBtn.DialogResult = DialogResult.Cancel;

        Controls.AddRange([scrobbleBtn, cancelBtn]);
        AcceptButton = scrobbleBtn;
        CancelButton = cancelBtn;
    }

    private static TextBox Input(int w, int h) => new()
    {
        Size        = new Size(w, h),
        BackColor   = Color.FromArgb(36, 36, 36),
        ForeColor   = Color.FromArgb(220, 220, 220),
        BorderStyle = BorderStyle.FixedSingle,
        Font        = FontManager.Regular(9.5f),
    };

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
}
