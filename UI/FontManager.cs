namespace LastFmScrobbler.UI;

internal static class FontManager
{
    // Prefer Segoe UI Variable (Win11) → Segoe UI → system default
    private static readonly string _family = ResolveFamily();

    private static string ResolveFamily()
    {
        var installed = new System.Drawing.Text.InstalledFontCollection();
        var names     = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[] { "Segoe UI Variable", "Segoe UI", "Arial" })
            if (names.Contains(candidate)) return candidate;

        return SystemFonts.DefaultFont.FontFamily.Name;
    }

    public static Font Regular(float size) => new(_family, size, FontStyle.Regular);
    public static Font Bold(float size)    => new(_family, size, FontStyle.Bold);
    public static Font Italic(float size)  => new(_family, size, FontStyle.Italic);
}
