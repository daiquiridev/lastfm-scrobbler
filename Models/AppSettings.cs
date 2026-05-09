namespace LastFmScrobbler.Models;

public class AppSettings
{
    public string? SessionKey { get; set; }
    public string? Username { get; set; }

    // Scrobble threshold: 10–100 (percent of track duration)
    public int ScrobbleThresholdPercent { get; set; } = 50;

    // Also scrobble if played longer than this many seconds (Last.fm rule: 4 min cap)
    public int ScrobbleThresholdMaxSeconds { get; set; } = 240;

    // Minimum seconds before any scrobble (Last.fm rule: at least 30s)
    public int ScrobbleMinSeconds { get; set; } = 30;

    public bool AutoNormalize { get; set; } = true;
    public bool FilterAppleMusicOnly { get; set; } = true;
    public bool EditBeforeScrobble { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool ShowNowPlayingNotification { get; set; } = false;

    // 0 = disabled; skip scrobble if the same track was scrobbled within this window
    public int DuplicateWindowMinutes { get; set; } = 5;

    public string AccentColor { get; set; } = "#BA0000";

    public string Language { get; set; } = "en";

    public int DailyScrobbleGoal { get; set; } = 0;
}
