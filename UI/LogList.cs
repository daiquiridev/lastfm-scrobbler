using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LastFmScrobbler.UI;

public enum LogKind
{
    NowPlaying,
    Scrobbled,
    Loved,
    Unloved,
    Failed,
    Manual,
    QueueFlushed,
}

public record LogEntry(LogKind Kind, string Text, DateTime Time);

public class LogList : ListBox
{
    private const int RowHeight = 40;
    private const int MaxItems  = 50;

    public Color Accent { get; set; } = Color.FromArgb(186, 0, 0);

    public LogList()
    {
        DrawMode      = DrawMode.OwnerDrawFixed;
        ItemHeight    = RowHeight;
        BackColor     = Color.FromArgb(18, 18, 18);
        BorderStyle   = BorderStyle.None;
        SelectionMode = SelectionMode.None;
        Font          = FontManager.Regular(9.5f);
        IntegralHeight = false;
    }

    public void AddEntry(LogKind kind, string text)
    {
        BeginUpdate();
        Items.Insert(0, new LogEntry(kind, text, DateTime.Now));
        if (Items.Count > MaxItems) Items.RemoveAt(MaxItems);
        EndUpdate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        if (Items[e.Index] is not LogEntry entry) return;

        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bounds = e.Bounds;

        using (var bg = new SolidBrush(BackColor))
            g.FillRectangle(bg, bounds);

        var (color, label) = GetKindStyle(entry.Kind);

        // ── Colored dot (left)
        const int dotSize = 8;
        var dotRect = new Rectangle(
            bounds.X + 16,
            bounds.Y + (bounds.Height - dotSize) / 2,
            dotSize,
            dotSize);
        using (var dotBrush = new SolidBrush(color))
            g.FillEllipse(dotBrush, dotRect);

        // ── Right side: time
        int textX         = dotRect.Right + 14;
        int rightBoundary = bounds.Right - 16;

        var timeStr  = entry.Time.ToString("HH:mm");
        var timeFont = FontManager.Regular(8.5f);
        var timeSize = TextRenderer.MeasureText(timeStr, timeFont);
        using (var timeBrush = new SolidBrush(Color.FromArgb(75, 75, 75)))
            g.DrawString(timeStr, timeFont, timeBrush,
                rightBoundary - timeSize.Width,
                bounds.Y + (bounds.Height - timeSize.Height) / 2);
        rightBoundary -= timeSize.Width + 14;

        // ── Right side: status label
        if (!string.IsNullOrEmpty(label))
        {
            var labelFont = FontManager.Regular(8.5f);
            var labelSize = TextRenderer.MeasureText(label, labelFont);
            using (var labelBrush = new SolidBrush(Color.FromArgb(130, 130, 130)))
                g.DrawString(label, labelFont, labelBrush,
                    rightBoundary - labelSize.Width,
                    bounds.Y + (bounds.Height - labelSize.Height) / 2);
            rightBoundary -= labelSize.Width + 12;
        }

        // ── Track text
        var textRect = new Rectangle(textX, bounds.Y, Math.Max(20, rightBoundary - textX), bounds.Height);
        using (var textBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
        {
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming      = StringTrimming.EllipsisCharacter,
                FormatFlags   = StringFormatFlags.NoWrap,
            };
            g.DrawString(entry.Text, FontManager.Regular(9.5f), textBrush, textRect, sf);
        }

        // ── Bottom separator
        using (var sep = new Pen(Color.FromArgb(28, 28, 28)))
            g.DrawLine(sep, bounds.X + 16, bounds.Bottom - 1, bounds.Right - 16, bounds.Bottom - 1);
    }

    private (Color color, string label) GetKindStyle(LogKind kind) => kind switch
    {
        LogKind.NowPlaying   => (Accent,                        "Now playing"),
        LogKind.Scrobbled    => (Color.FromArgb(80, 200, 80),   "Scrobbled"),
        LogKind.Loved        => (Color.FromArgb(220, 80, 110),  "Loved"),
        LogKind.Unloved      => (Color.FromArgb(110, 110, 110), "Unloved"),
        LogKind.Failed       => (Color.FromArgb(220, 80, 80),   "Failed"),
        LogKind.Manual       => (Color.FromArgb(120, 160, 220), "Manual"),
        LogKind.QueueFlushed => (Color.FromArgb(150, 150, 150), "Queue"),
        _                    => (Color.FromArgb(100, 100, 100), ""),
    };
}
