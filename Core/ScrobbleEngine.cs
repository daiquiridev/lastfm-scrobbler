using LastFmScrobbler.Data;
using LastFmScrobbler.Models;

namespace LastFmScrobbler.Core;

/// <summary>
/// Orchestrates: media detection → normalization → timing → scrobbling.
/// </summary>
public class ScrobbleEngine : IDisposable
{
    public event EventHandler<Track?>? NowPlayingChanged;
    public event EventHandler<(Track track, bool success)>? TrackScrobbled;
    public event EventHandler<int>? PendingQueueFlushed; // arg = number of scrobbles sent

    private readonly Database _db;
    private readonly MediaWatcher _watcher;
    private readonly TrackNormalizer _normalizer;
    private readonly LastFmClient _lfm;

    private AppSettings _settings;
    private Track? _currentTrack;
    private DateTime _trackStartedAt;
    private System.Threading.Timer? _scrobbleTimer;
    private System.Threading.Timer? _retryTimer;
    private int _trackGeneration;
    private readonly object _lock = new();

    // Raised when the engine wants the UI to confirm/edit a track before scrobbling.
    public Func<Track, Task<bool>>? ConfirmBeforeScrobble { get; set; }

    public ScrobbleEngine(Database db, AppSettings settings)
    {
        _db = db;
        _settings = settings;

        _normalizer = new TrackNormalizer();
        ReloadRules();

        _watcher = new MediaWatcher();
        _watcher.TrackChanged += OnTrackChanged;

        _lfm = new LastFmClient();
        ApplyCredentials();
    }

    public void ApplyCredentials()
    {
        _lfm.Configure(_settings.SessionKey);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        ApplyCredentials();
        _watcher.UpdateFilter(settings.FilterAppleMusicOnly);
        ReloadRules();
    }

    public void ReloadRules()
    {
        var rules = _db.LoadRules();
        _normalizer.UpdateRules(rules);
    }

    public async Task StartAsync()
    {
        await _watcher.StartAsync(_settings.FilterAppleMusicOnly);
        _ = FlushPendingQueueAsync();
        _retryTimer = new System.Threading.Timer(
            _ => _ = FlushPendingQueueAsync(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    public bool IsAuthenticated => _lfm.IsAuthenticated;
    public LastFmClient LastFmClient => _lfm;
    public Track? CurrentTrack => _currentTrack;

    // ── Track Change ─────────────────────────────────────────────────────────

    private void OnTrackChanged(object? sender, Track? rawTrack)
    {
        lock (_lock)
        {
            CancelScrobbleTimer();
            if (rawTrack is null)
            {
                _currentTrack = null;
                NowPlayingChanged?.Invoke(this, null);
                return;
            }

            var track = _settings.AutoNormalize ? _normalizer.Normalize(rawTrack) : rawTrack;
            _currentTrack = track;
            _trackStartedAt = DateTime.UtcNow;
            var gen = ++_trackGeneration;

            NowPlayingChanged?.Invoke(this, track);
            _ = ResolveAlbumThenProceedAsync(track, gen);
        }
    }

    private async Task ResolveAlbumThenProceedAsync(Track track, int gen)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(track.Album))
            {
                var lfmAlbum = await _lfm.GetAlbumNameAsync(track.Artist, track.Title);
                if (!string.IsNullOrWhiteSpace(lfmAlbum))
                {
                    lock (_lock)
                    {
                        if (gen == _trackGeneration)
                        {
                            track.Album = lfmAlbum;
                            NowPlayingChanged?.Invoke(this, track);
                        }
                    }
                }
            }
        }
        catch { /* keep SMTC album as fallback */ }

        lock (_lock)
        {
            if (gen != _trackGeneration) return;
        }

        await SendNowPlayingAsync(track);
        lock (_lock)
        {
            if (gen == _trackGeneration)
                ScheduleScrobble(track);
        }
    }

    private async Task SendNowPlayingAsync(Track track)
    {
        if (!_lfm.IsAuthenticated) return;
        try { await _lfm.UpdateNowPlayingAsync(track); }
        catch { /* not critical */ }
    }

    // ── Scrobble Timing ──────────────────────────────────────────────────────

    private void ScheduleScrobble(Track track)
    {
        int thresholdMs = CalculateThresholdMs(track);
        if (thresholdMs <= 0) return;

        _scrobbleTimer = new System.Threading.Timer(
            _ => _ = DoScrobbleAsync(track, _trackStartedAt),
            null,
            thresholdMs,
            Timeout.Infinite);
    }

    public int GetScrobbleThresholdMs(Track track) => CalculateThresholdMs(track);

    private int CalculateThresholdMs(Track track)
    {
        int minMs = _settings.ScrobbleMinSeconds * 1000;

        if (track.DurationSeconds.HasValue && track.DurationSeconds > 0)
        {
            int byPercent = (int)(track.DurationSeconds.Value * 1000 * _settings.ScrobbleThresholdPercent / 100.0);
            int byMax = _settings.ScrobbleThresholdMaxSeconds * 1000;
            return Math.Max(minMs, Math.Min(byPercent, byMax));
        }

        return Math.Max(minMs, (int)(_settings.ScrobbleThresholdMaxSeconds * 1000 * _settings.ScrobbleThresholdPercent / 100.0));
    }

    private void CancelScrobbleTimer()
    {
        _scrobbleTimer?.Dispose();
        _scrobbleTimer = null;
    }

    // ── Scrobble ─────────────────────────────────────────────────────────────

    private async Task DoScrobbleAsync(Track track, DateTime startedAt)
    {
        lock (_lock)
        {
            if (!track.IsSameTrack(_currentTrack)) return;
        }

        Track scrobbleTrack = track;

        if (_settings.EditBeforeScrobble && ConfirmBeforeScrobble is not null)
        {
            var proceed = await ConfirmBeforeScrobble(scrobbleTrack);
            if (!proceed) return;
        }

        // Duplicate suppression
        if (_settings.DuplicateWindowMinutes > 0 &&
            _db.WasRecentlyScrobbled(scrobbleTrack.Artist, scrobbleTrack.Title, _settings.DuplicateWindowMinutes))
        {
            return;
        }

        bool success = false;
        string? error = null;

        try
        {
            if (_lfm.IsAuthenticated)
                success = await _lfm.ScrobbleAsync(scrobbleTrack, startedAt);
        }
        catch (System.Net.Http.HttpRequestException)
        {
            // Network failure — queue for later retry
            _db.AddPendingScrobble(new PendingScrobble
            {
                Title     = scrobbleTrack.Title,
                Artist    = scrobbleTrack.Artist,
                Album     = scrobbleTrack.Album,
                PlayedAt  = startedAt,
                QueuedAt  = DateTime.UtcNow,
            });
            error = "Offline — queued for retry";
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        _db.AddScrobbleRecord(new ScrobbleRecord
        {
            Title        = scrobbleTrack.Title,
            Artist       = scrobbleTrack.Artist,
            Album        = scrobbleTrack.Album,
            ScrobbledAt  = DateTime.UtcNow,
            Success      = success,
            ErrorMessage = error
        });

        TrackScrobbled?.Invoke(this, (scrobbleTrack, success));
    }

    // ── Manual Scrobble ───────────────────────────────────────────────────────

    public async Task<bool> ManualScrobbleAsync(string artist, string title, string album, DateTime playedAt)
    {
        var track  = new Track { Artist = artist, Title = title, Album = album };
        bool success = false;
        string? error = null;

        try
        {
            if (_lfm.IsAuthenticated)
                success = await _lfm.ScrobbleAsync(track, playedAt);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        _db.AddScrobbleRecord(new ScrobbleRecord
        {
            Title        = title,
            Artist       = artist,
            Album        = album,
            ScrobbledAt  = DateTime.UtcNow,
            Success      = success,
            ErrorMessage = error
        });

        TrackScrobbled?.Invoke(this, (track, success));
        return success;
    }

    // ── Love / Unlove ─────────────────────────────────────────────────────────

    public async Task LoveTrackAsync(Track track, bool love)
    {
        try
        {
            if (love) await _lfm.LoveTrackAsync(track.Artist, track.Title);
            else      await _lfm.UnloveTrackAsync(track.Artist, track.Title);
        }
        catch { /* best-effort */ }
    }

    // ── Offline Queue Flush ───────────────────────────────────────────────────

    public async Task FlushPendingQueueAsync()
    {
        if (!_lfm.IsAuthenticated) return;

        var pending = _db.LoadPendingScrobbles();
        if (pending.Count == 0) return;

        var items = pending
            .Select(p => (new Track { Artist = p.Artist, Title = p.Title, Album = p.Album }, p.PlayedAt))
            .ToList();

        try
        {
            int sent = await _lfm.ScrobbleBatchAsync(items);
            foreach (var p in pending)
                _db.DeletePendingScrobble(p.Id);

            if (sent > 0)
                PendingQueueFlushed?.Invoke(this, sent);
        }
        catch { /* still offline — try again next tick */ }
    }

    // ── Album Art ─────────────────────────────────────────────────────────────

    public Task<System.Drawing.Image?> GetCurrentThumbnailAsync() =>
        _watcher.GetCurrentThumbnailAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        CancelScrobbleTimer();
        _retryTimer?.Dispose();
        _watcher.Dispose();
    }
}
