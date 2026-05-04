using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LastFmScrobbler.UI;

internal static class FontManager
{
    private static readonly PrivateFontCollection _pfc = new();
    private static FontFamily? _geist;
    private static bool _loaded;

    private static FontFamily GetGeist()
    {
        if (!_loaded) Load();
        return _geist ?? SystemFonts.DefaultFont.FontFamily;
    }

    private static void Load()
    {
        _loaded = true;
        LoadFont("LastFmScrobbler.Fonts.Geist-Regular.ttf");
        LoadFont("LastFmScrobbler.Fonts.Geist-Bold.ttf");
        _geist = _pfc.Families.FirstOrDefault(f =>
            f.Name.StartsWith("Geist", StringComparison.OrdinalIgnoreCase) &&
            !f.Name.Contains("Mono", StringComparison.OrdinalIgnoreCase));
    }

    private static void LoadFont(string resourceName)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null) return;
            var data = new byte[stream.Length];
            _ = stream.Read(data, 0, data.Length);
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try { _pfc.AddMemoryFont(handle.AddrOfPinnedObject(), data.Length); }
            finally { handle.Free(); }
        }
        catch { }
    }

    public static Font Regular(float size) => new(GetGeist(), size, FontStyle.Regular);
    public static Font Bold(float size)    => new(GetGeist(), size, FontStyle.Bold);
    public static Font Italic(float size)  => new(GetGeist(), size, FontStyle.Italic);
}
