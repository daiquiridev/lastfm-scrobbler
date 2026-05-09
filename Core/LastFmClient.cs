using System.Net.Http;
using LastFmScrobbler.Models;
using Newtonsoft.Json.Linq;

namespace LastFmScrobbler.Core;

public class LastFmClient
{
    // Update this constant after deploying the Cloudflare Worker.
    private const string WorkerUrl = "https://proxy.lastfm.spacechild.dev/";

    private readonly HttpClient _http = new();
    private string? _sessionKey;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_sessionKey);

    public void Configure(string? sessionKey)
    {
        _sessionKey = sessionKey;
    }

    // ── Auth Flow ────────────────────────────────────────────────────────────

    public async Task<(string token, string authUrl)> GetAuthUrlAsync()
    {
        var result = await CallAsync(new Dictionary<string, string>
        {
            ["method"] = "auth.getToken",
        }, signed: false);

        var token = result["token"]?.ToString()
            ?? throw new Exception("No token returned from Last.fm");
        var url = result["_authUrl"]?.ToString()
            ?? throw new Exception("No auth URL returned from proxy");

        return (token, url);
    }

    public async Task<(string sessionKey, string username)> GetSessionAsync(string token)
    {
        var result = await CallAsync(new Dictionary<string, string>
        {
            ["method"] = "auth.getSession",
            ["token"]  = token,
        }, signed: true);

        var session = result["session"]
            ?? throw new Exception("No session in response");

        var sk   = session["key"]?.ToString()  ?? throw new Exception("No session key");
        var name = session["name"]?.ToString() ?? string.Empty;

        _sessionKey = sk;
        return (sk, name);
    }

    // ── Track Info ───────────────────────────────────────────────────────────

    public async Task<string?> GetAlbumNameAsync(string artist, string title)
    {
        try
        {
            var result = await CallAsync(new Dictionary<string, string>
            {
                ["method"]      = "track.getInfo",
                ["artist"]      = artist,
                ["track"]       = title,
                ["autocorrect"] = "1",
            }, signed: false);

            return result.SelectToken("track.album.title")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    // ── Love / Unlove ────────────────────────────────────────────────────────

    public async Task LoveTrackAsync(string artist, string title)
    {
        if (!IsAuthenticated) return;
        await CallAsync(new Dictionary<string, string>
        {
            ["method"] = "track.love",
            ["sk"]     = _sessionKey!,
            ["artist"] = artist,
            ["track"]  = title,
        }, signed: true);
    }

    public async Task UnloveTrackAsync(string artist, string title)
    {
        if (!IsAuthenticated) return;
        await CallAsync(new Dictionary<string, string>
        {
            ["method"] = "track.unlove",
            ["sk"]     = _sessionKey!,
            ["artist"] = artist,
            ["track"]  = title,
        }, signed: true);
    }

    // ── Scrobbling ───────────────────────────────────────────────────────────

    public async Task UpdateNowPlayingAsync(Track track)
    {
        if (!IsAuthenticated) return;

        var p = new Dictionary<string, string>
        {
            ["method"] = "track.updateNowPlaying",
            ["sk"]     = _sessionKey!,
            ["artist"] = track.Artist,
            ["track"]  = track.Title,
        };
        if (!string.IsNullOrEmpty(track.Album)) p["album"] = track.Album;
        if (track.DurationSeconds.HasValue) p["duration"] = track.DurationSeconds.Value.ToString();

        await CallAsync(p, signed: true);
    }

    public async Task<bool> ScrobbleAsync(Track track, DateTime playedAt)
    {
        if (!IsAuthenticated) return false;

        var p = new Dictionary<string, string>
        {
            ["method"]       = "track.scrobble",
            ["sk"]           = _sessionKey!,
            ["artist[0]"]    = track.Artist,
            ["track[0]"]     = track.Title,
            ["timestamp[0]"] = ((DateTimeOffset)playedAt.ToUniversalTime()).ToUnixTimeSeconds().ToString(),
        };
        if (!string.IsNullOrEmpty(track.Album)) p["album[0]"] = track.Album;
        if (track.DurationSeconds.HasValue) p["duration[0]"] = track.DurationSeconds.Value.ToString();

        var result   = await CallAsync(p, signed: true);
        var accepted = result.SelectToken("scrobbles.@attr.accepted");
        return accepted?.Value<int>() == 1;
    }

    public async Task<int> ScrobbleBatchAsync(List<(Track track, DateTime playedAt)> items)
    {
        if (!IsAuthenticated || items.Count == 0) return 0;

        var p = new Dictionary<string, string>
        {
            ["method"] = "track.scrobble",
            ["sk"]     = _sessionKey!,
        };

        for (int i = 0; i < items.Count; i++)
        {
            var (track, playedAt) = items[i];
            p[$"artist[{i}]"]    = track.Artist;
            p[$"track[{i}]"]     = track.Title;
            p[$"timestamp[{i}]"] = ((DateTimeOffset)playedAt.ToUniversalTime()).ToUnixTimeSeconds().ToString();
            if (!string.IsNullOrEmpty(track.Album)) p[$"album[{i}]"] = track.Album;
        }

        var result   = await CallAsync(p, signed: true);
        var accepted = result.SelectToken("scrobbles.@attr.accepted");
        return accepted?.Value<int>() ?? 0;
    }

    // ── Artist Info ──────────────────────────────────────────────────────────

    public async Task<(string bio, string[] similar)> GetArtistInfoAsync(string artist)
    {
        try
        {
            var result = await CallAsync(new Dictionary<string, string>
            {
                ["method"]      = "artist.getInfo",
                ["artist"]      = artist,
                ["autocorrect"] = "1",
            }, signed: false);
            var bio = result.SelectToken("artist.bio.summary")?.ToString() ?? "";
            bio = System.Text.RegularExpressions.Regex.Replace(bio, @"<a\s[^>]*>.*?</a>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            bio = System.Text.RegularExpressions.Regex.Replace(bio, "<[^>]+>", "").Trim();
            var nl = bio.IndexOf('\n'); if (nl > 0) bio = bio[..nl].Trim();
            if (bio.Length > 280) bio = bio[..280].TrimEnd() + "…";
            var similar = result.SelectToken("artist.similar.artist")
                ?.Select(t => t["name"]?.ToString() ?? "").Where(n => n.Length > 0).Take(4).ToArray() ?? [];
            return (bio, similar);
        }
        catch { return ("", []); }
    }

    public async Task<string[]> GetArtistTopTagsAsync(string artist)
    {
        try
        {
            var result = await CallAsync(new Dictionary<string, string>
            {
                ["method"]      = "artist.getTopTags",
                ["artist"]      = artist,
                ["autocorrect"] = "1",
            }, signed: false);
            return result.SelectToken("toptags.tag")
                ?.Select(t => t["name"]?.ToString() ?? "").Where(n => n.Length > 0).Take(5).ToArray() ?? [];
        }
        catch { return []; }
    }

    // ── User Stats ───────────────────────────────────────────────────────────

    public async Task<(string name, int playcount)[]> GetUserTopArtistsAsync(string username, string period, int limit = 10)
    {
        try
        {
            var result = await CallAsync(new Dictionary<string, string>
            {
                ["method"] = "user.getTopArtists",
                ["user"]   = username,
                ["period"] = period,
                ["limit"]  = limit.ToString(),
            }, signed: false);
            return result.SelectToken("topartists.artist")
                ?.Select(t => (t["name"]?.ToString() ?? "", int.TryParse(t["playcount"]?.ToString(), out var pc) ? pc : 0))
                .Where(x => x.Item1.Length > 0).ToArray() ?? [];
        }
        catch { return []; }
    }

    public async Task<(string artist, string name, int playcount)[]> GetUserTopTracksAsync(string username, string period, int limit = 10)
    {
        try
        {
            var result = await CallAsync(new Dictionary<string, string>
            {
                ["method"] = "user.getTopTracks",
                ["user"]   = username,
                ["period"] = period,
                ["limit"]  = limit.ToString(),
            }, signed: false);
            return result.SelectToken("toptracks.track")
                ?.Select(t => (
                    t["artist"]?["name"]?.ToString() ?? "",
                    t["name"]?.ToString() ?? "",
                    int.TryParse(t["playcount"]?.ToString(), out var pc) ? pc : 0))
                .Where(x => x.Item2.Length > 0).ToArray() ?? [];
        }
        catch { return []; }
    }

    // ── Friends ──────────────────────────────────────────────────────────────

    public async Task<(string name, string artist, string track, bool nowPlaying)[]> GetFriendsAsync(string username)
    {
        try
        {
            var result = await CallAsync(new Dictionary<string, string>
            {
                ["method"]       = "user.getFriends",
                ["user"]         = username,
                ["recenttracks"] = "1",
                ["limit"]        = "50",
            }, signed: false);
            var users = result.SelectToken("friends.user");
            if (users is null) return [];
            return users.Select(u =>
            {
                var name   = u["name"]?.ToString() ?? "";
                var rt     = u["recenttrack"];
                var artist = rt?["artist"]?["#text"]?.ToString() ?? "";
                var track  = rt?["name"]?.ToString() ?? "";
                var np     = rt?["@attr"]?["nowplaying"]?.ToString() == "true";
                return (name, artist, track, np);
            }).Where(x => x.name.Length > 0).ToArray();
        }
        catch { return []; }
    }

    // ── HTTP ─────────────────────────────────────────────────────────────────

    private async Task<JObject> CallAsync(Dictionary<string, string> parameters, bool signed)
    {
        if (signed) parameters["_sign"] = "1";

        var content = new FormUrlEncodedContent(parameters.Select(p =>
            new KeyValuePair<string, string>(p.Key, p.Value)));

        var response = await _http.PostAsync(WorkerUrl, content);
        var json     = await response.Content.ReadAsStringAsync();
        var obj      = JObject.Parse(json);

        if (obj["error"] is JToken err)
            throw new Exception($"Last.fm error {err}: {obj["message"]}");

        return obj;
    }
}
