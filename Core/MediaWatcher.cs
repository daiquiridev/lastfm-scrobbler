using LastFmScrobbler.Models;
using Windows.Media.Control;

namespace LastFmScrobbler.Core;

/// <summary>
/// Listens to Windows System Media Transport Controls (SMTC) and raises
/// events when the playing track changes or playback stops.
/// </summary>
public class MediaWatcher : IDisposable
{
    public event EventHandler<Track?>? TrackChanged;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _filterAppleMusicOnly;
    private Track? _lastTrack;
    private readonly object _raiseLock = new();
    private bool _disposed;

    // Apple Music briefly emits Stopped between tracks (~200-800 ms). If we
    // propagate that null immediately, the engine clears _currentTrack and a
    // duplicate "now playing" log entry shows up when the next event arrives.
    // Debounce nulls so a real track that follows can cancel the empty event.
    private System.Threading.Timer? _nullDebounce;
    private const int NullDebounceMs = 1500;

    public async Task StartAsync(bool filterAppleMusicOnly)
    {
        _filterAppleMusicOnly = filterAppleMusicOnly;
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += OnSessionChanged;
        _manager.SessionsChanged += OnSessionsChanged;
        await RefreshSessionAsync();
    }

    public void UpdateFilter(bool appleOnly)
    {
        _filterAppleMusicOnly = appleOnly;
        _ = RefreshSessionAsync();
    }

    private void OnSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => _ = RefreshSessionAsync();

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => _ = RefreshSessionAsync();

    private async Task RefreshSessionAsync()
    {
        if (_manager is null) return;

        // Unsubscribe from old session
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }

        _session = FindBestSession(_manager);

        if (_session is null)
        {
            RaiseTrackChanged(null);
            return;
        }

        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        await FetchAndRaiseAsync();
    }

    private GlobalSystemMediaTransportControlsSession? FindBestSession(
        GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var sessions = manager.GetSessions();

        if (_filterAppleMusicOnly)
        {
            // Apple Music on Windows: app ID varies by OS version and install method.
            // Windows 11 Store: "AppleInc.AppleMusic", Windows 10 Store: "AppleMusic.exe"
            var appleSession = sessions.FirstOrDefault(s =>
                s.SourceAppUserModelId.Contains("AppleInc.AppleMusic", StringComparison.OrdinalIgnoreCase) ||
                s.SourceAppUserModelId.Contains("AppleMusic.exe", StringComparison.OrdinalIgnoreCase) ||
                s.SourceAppUserModelId.Contains("iTunes", StringComparison.OrdinalIgnoreCase) ||
                s.SourceAppUserModelId.Contains("Apple Music", StringComparison.OrdinalIgnoreCase));

            return appleSession;
        }

        // Otherwise use the current active session
        return manager.GetCurrentSession();
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => _ = FetchAndRaiseAsync();

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => _ = FetchAndRaiseAsync();

    private async Task FetchAndRaiseAsync()
    {
        if (_session is null) return;

        try
        {
            var info = _session.GetPlaybackInfo();

            if (info.PlaybackStatus is
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped or
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
            {
                RaiseTrackChanged(null);
                return;
            }

            var props = await _session.TryGetMediaPropertiesAsync();
            if (props is null)
            {
                RaiseTrackChanged(null);
                return;
            }

            var rawArtistField = props.Artist      ?? string.Empty;
            var rawAlbumField  = props.AlbumTitle  ?? string.Empty;
            var albumArtist    = props.AlbumArtist ?? string.Empty;
            var sourceApp      = _session.SourceAppUserModelId;

            bool isAppleMusic = sourceApp.Contains("Apple", StringComparison.OrdinalIgnoreCase);

            string artist, album;

            if (isAppleMusic)
            {
                // Apple Music on Windows puts "AlbumArtist — AlbumTitle" into the SMTC Artist field.
                // AlbumTitle field is usually empty. We split on em/en dash to recover both.
                (artist, album) = ParseAppleMusicArtistField(rawArtistField, albumArtist, rawAlbumField);
            }
            else
            {
                artist = rawArtistField;
                album  = rawAlbumField;
            }

            var track = new Track
            {
                Title       = props.Title ?? string.Empty,
                Artist      = artist,
                Album       = album,
                RawAlbum    = rawArtistField, // store the raw combined field for monitor display
                AlbumArtist = string.IsNullOrEmpty(albumArtist) ? null : albumArtist,
                TrackNumber = (int?)props.TrackNumber,
                SourceApp   = sourceApp,
                DetectedAt  = DateTime.UtcNow
            };

            // Try to get duration from timeline properties
            var timeline = _session.GetTimelineProperties();
            if (timeline?.EndTime.TotalSeconds > 0)
                track.DurationSeconds = (int)timeline.EndTime.TotalSeconds;

            if (!track.IsValid) return;

            RaiseTrackChanged(track);
        }
        catch
        {
            // Session may have gone away
            RaiseTrackChanged(null);
        }
    }

    /// <summary>
    /// Apple Music on Windows puts "AlbumArtist — AlbumTitle" into the SMTC Artist field.
    /// Splits on em dash / en dash to extract the real artist and album separately.
    /// Falls back to albumArtist field if available and matches.
    /// </summary>
    private static (string artist, string album) ParseAppleMusicArtistField(
        string rawArtistField, string albumArtist, string rawAlbumField)
    {
        // Try em dash first (U+2014), then en dash (U+2013), both with and without surrounding spaces
        string[] separators = [" \u2014 ", "\u2014", " \u2013 ", "\u2013"];

        foreach (var sep in separators)
        {
            var idx = rawArtistField.IndexOf(sep, StringComparison.Ordinal);
            if (idx <= 0) continue;

            var artistPart = rawArtistField[..idx].Trim();
            var albumPart  = rawArtistField[(idx + sep.Length)..].Trim();

            // AlbumArtist field is also "Artist — Album" combined, so always use the split left-side
            var finalArtist = artistPart;

            // Prefer parsed album; fall back to SMTC AlbumTitle if both are somehow present
            var finalAlbum = string.IsNullOrWhiteSpace(albumPart) ? rawAlbumField : albumPart;

            return (finalArtist, finalAlbum);
        }

        // No separator found — Artist field is just the artist, use AlbumTitle for album
        var artist = string.IsNullOrWhiteSpace(albumArtist) ? rawArtistField : albumArtist;
        return (artist.Trim(), rawAlbumField.Trim());
    }

    private void RaiseTrackChanged(Track? track)
    {
        if (track is null)
        {
            // Don't propagate yet — schedule a delayed fire that a real track
            // arriving within NullDebounceMs can cancel.
            lock (_raiseLock)
            {
                if (_lastTrack is null) return; // already in null state, nothing to debounce
                _nullDebounce?.Dispose();
                _nullDebounce = new System.Threading.Timer(_ =>
                {
                    Track? capturedLast;
                    lock (_raiseLock)
                    {
                        capturedLast = _lastTrack;
                        if (capturedLast is null) return;
                        _lastTrack = null;
                        _nullDebounce?.Dispose();
                        _nullDebounce = null;
                    }
                    TrackChanged?.Invoke(this, null);
                }, null, NullDebounceMs, System.Threading.Timeout.Infinite);
            }
            return;
        }

        lock (_raiseLock)
        {
            // A real track arrived — cancel any pending null fire.
            _nullDebounce?.Dispose();
            _nullDebounce = null;

            if (track.IsSameTrack(_lastTrack)) return;
            _lastTrack = track;
        }
        TrackChanged?.Invoke(this, track);
    }

    public async Task<System.Drawing.Image?> GetCurrentThumbnailAsync()
    {
        if (_session is null) return null;
        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            if (props?.Thumbnail is null) return null;

            using var stream = await props.Thumbnail.OpenReadAsync();
            if (stream is null || stream.Size == 0) return null;

            using var reader = new Windows.Storage.Streams.DataReader(stream);
            var size = (uint)stream.Size;
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);

            using var ms  = new MemoryStream(bytes);
            using var tmp = System.Drawing.Image.FromStream(ms);
            return new System.Drawing.Bitmap(tmp);
        }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _nullDebounce?.Dispose();
        _nullDebounce = null;

        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }
    }
}
