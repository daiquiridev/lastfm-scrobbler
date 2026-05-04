using LastFmScrobbler.Models;

namespace LastFmScrobbler.UI;

public class EditTrackForm : Form
{
    private readonly Track _track;
    private TextBox _titleBox  = null!;
    private TextBox _artistBox = null!;
    private TextBox _albumBox  = null!;

    public EditTrackForm(Track track)
    {
        _track = track;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text            = "Edit track before scrobbling";
        Size            = new Size(460, 250);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = Color.FromArgb(28, 28, 28);
        ForeColor       = Color.FromArgb(220, 220, 220);
        Font            = FontManager.Regular(9.5f);

        int y = 18;

        Controls.Add(MakeLabel("Title:",  14, y + 4));
        _titleBox = Input(300, 26); _titleBox.Text = _track.Title;
        _titleBox.Location = new Point(110, y);
        Controls.Add(_titleBox);
        y += 38;

        Controls.Add(MakeLabel("Artist:", 14, y + 4));
        _artistBox = Input(300, 26); _artistBox.Text = _track.Artist;
        _artistBox.Location = new Point(110, y);
        Controls.Add(_artistBox);
        y += 38;

        Controls.Add(MakeLabel("Album:",  14, y + 4));
        _albumBox = Input(300, 26); _albumBox.Text = _track.Album;
        _albumBox.Location = new Point(110, y);
        Controls.Add(_albumBox);
        y += 50;

        var scrobbleBtn = MakeBtn("Scrobble", 110, 34);
        scrobbleBtn.Location    = new Point(110, y);
        scrobbleBtn.DialogResult = DialogResult.OK;
        scrobbleBtn.BackColor   = Color.FromArgb(186, 0, 0);
        scrobbleBtn.Click += (_, _) =>
        {
            _track.Title  = _titleBox.Text.Trim();
            _track.Artist = _artistBox.Text.Trim();
            _track.Album  = _albumBox.Text.Trim();
        };

        var skipBtn = MakeBtn("Skip", 86, 34);
        skipBtn.Location    = new Point(230, y);
        skipBtn.DialogResult = DialogResult.Cancel;

        Controls.AddRange([scrobbleBtn, skipBtn]);
        AcceptButton = scrobbleBtn;
        CancelButton = skipBtn;
    }

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(90, 20),
        ForeColor = Color.FromArgb(110, 110, 110),
        TextAlign = ContentAlignment.MiddleLeft,
    };

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
