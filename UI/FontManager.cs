using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LastFmScrobbler.UI;

internal static class FontManager
{
    private static readonly PrivateFontCollection _pfc = new();
    private static readonly List<GCHandle> _handles = new();
    private static bool _loaded;
    private static string _familyName = "Segoe UI";

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadFont("LastFmScrobbler.Fonts.Geist-Regular.ttf");
        LoadFont("LastFmScrobbler.Fonts.Geist-Bold.ttf");
        var family = _pfc.Families.FirstOrDefault(f =>
            f.Name.StartsWith("Geist", StringComparison.OrdinalIgnoreCase) &&
            !f.Name.Contains("Mono", StringComparison.OrdinalIgnoreCase));
        if (family is not null)
            _familyName = family.Name;
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

            // Pin permanently — GDI may hold a reference to the memory
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            _handles.Add(handle);

            var ptr = handle.AddrOfPinnedObject();
            _pfc.AddMemoryFont(ptr, data.Length);           // GDI+ (Graphics.DrawString)
            uint count = 0;
            AddFontMemResourceEx(ptr, (uint)data.Length, IntPtr.Zero, ref count); // GDI (TextRenderer / controls)
        }
        catch { }
    }

    public static Font Regular(float size) { EnsureLoaded(); return new Font(_familyName, size, FontStyle.Regular); }
    public static Font Bold(float size)    { EnsureLoaded(); return new Font(_familyName, size, FontStyle.Bold);    }
    public static Font Italic(float size)  { EnsureLoaded(); return new Font(_familyName, size, FontStyle.Italic);  }
}
