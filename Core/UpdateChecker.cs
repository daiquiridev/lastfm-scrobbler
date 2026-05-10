using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace LastFmScrobbler.Core;

public class UpdateChecker
{
    public const string ManifestUrl  = "https://pub-8a5464b225534730b481b262ffe4748b.r2.dev/lastfm-scrobbler/latest.json";
    public const string VersionsUrl  = "https://pub-8a5464b225534730b481b262ffe4748b.r2.dev/lastfm-scrobbler/versions.json";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly Version _current =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public record UpdateInfo(string Version, string Url, string Sha256);
    public record VersionEntry(string Version, string Url, string Sha256, string Date);

    /// <summary>Returns update info if a newer version exists, or null if already up-to-date.</summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ManifestUrl);
            var obj  = JObject.Parse(json);

            var versionStr = obj["version"]?.ToString();
            var url        = obj["url"]?.ToString();
            var sha256     = obj["sha256"]?.ToString();

            if (versionStr is null || url is null || sha256 is null) return null;
            if (!Version.TryParse(versionStr, out var remote))     return null;

            // Normalize both to 3 components (Major.Minor.Build) so "1.2.2" == "1.2.2.0"
            var r3 = new Version(remote.Major,   remote.Minor,   Math.Max(remote.Build,    0));
            var c3 = new Version(_current.Major, _current.Minor, Math.Max(_current.Build,  0));
            return r3 > c3 ? new UpdateInfo(versionStr, url, sha256) : null;
        }
        catch { return null; }
    }

    /// <summary>Downloads the installer to %TEMP%, verifies SHA-256, returns the local path.</summary>
    public async Task<string> DownloadAsync(UpdateInfo info, IProgress<int> progress)
    {
        var dest = Path.Combine(
            Path.GetTempPath(),
            $"LastFmScrobbler-Setup-{info.Version}.exe");

        using var response = await _http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file   = File.Create(dest);

        var buf = new byte[65536];
        long done = 0;
        int  read;
        while ((read = await stream.ReadAsync(buf)) > 0)
        {
            await file.WriteAsync(buf.AsMemory(0, read));
            done += read;
            if (total > 0) progress.Report((int)(done * 100 / total));
        }

        // Verify integrity before running
        file.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
        if (!hash.Equals(info.Sha256.ToLowerInvariant(), StringComparison.Ordinal))
        {
            file.Close();
            File.Delete(dest);
            throw new InvalidDataException($"SHA-256 mismatch. Expected {info.Sha256}, got {hash}.");
        }

        return dest;
    }

    /// <summary>Returns all available versions from R2, newest first.</summary>
    public async Task<List<VersionEntry>> GetVersionHistoryAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(VersionsUrl);
            var arr  = JArray.Parse(json);
            return arr
                .Select(o => new VersionEntry(
                    o["version"]?.ToString() ?? "",
                    o["url"]?.ToString()     ?? "",
                    o["sha256"]?.ToString()  ?? "",
                    o["date"]?.ToString()    ?? ""))
                .Where(v => !string.IsNullOrEmpty(v.Version))
                .ToList();
        }
        catch { return []; }
    }

    /// <summary>Launches the installer silently and signals the caller to exit.</summary>
    public static void LaunchAndExit(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments       = "/verysilent /closeapplications /restartapplications",
        });
    }
}
