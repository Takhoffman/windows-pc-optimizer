using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Velocity.Core;

public sealed record UpdateInfo(Version Version, string TagName, string DownloadUrl, string Notes);

/// <summary>
/// Checks GitHub Releases for a newer version, downloads its MSI, and hands off
/// to the Windows Installer. There is no background service or scheduled task —
/// the check runs once, on demand, from the app itself.
/// </summary>
public static class UpdateService
{
    private const string Owner = "takhoffman";
    private const string Repo = "windows-pc-optimizer";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        // GitHub's API requires a User-Agent.
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Velocity-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return Normalize(v);
        }
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    private static Version? ParseTag(string tag)
    {
        tag = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(tag, out var v) ? Normalize(v) : null;
    }

    /// <summary>
    /// Returns details of a newer release if one exists, otherwise null.
    /// Never throws — any failure (offline, rate-limited, malformed) yields null.
    /// <paramref name="currentOverride"/> exists for testing; production uses the assembly version.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(Version? currentOverride = null)
    {
        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var d) && d.GetBoolean()) return null;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var version = ParseTag(tag);
            if (version is null) return null;

            var current = currentOverride is null ? CurrentVersion : Normalize(currentOverride);
            if (version <= current) return null;

            string? msiUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        msiUrl = a.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(msiUrl)) return null;

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            return new UpdateInfo(version, tag, msiUrl, notes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Downloads the release MSI to a temp file, reporting fractional progress.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct = default)
    {
        var dest = Path.Combine(Path.GetTempPath(), $"VelocitySetup-{info.Version}.msi");

        using var resp = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;

        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(dest))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                readTotal += n;
                if (total > 0) progress?.Report((double)readTotal / total);
            }
        }
        return dest;
    }

    /// <summary>
    /// Spawns a detached helper that waits for this process to exit, silently
    /// installs the MSI (a major upgrade over the running install), then relaunches
    /// the app. Call <see cref="System.Windows.Application.Shutdown()"/> right after.
    /// </summary>
    public static void InstallAndRelaunch(string msiPath, string exePath)
    {
        var pid = Environment.ProcessId;
        var script =
            $"Wait-Process -Id {pid} -Timeout 60 -ErrorAction SilentlyContinue; " +
            $"Start-Process msiexec -ArgumentList '/i','\"{msiPath}\"','/qb','/norestart' -Wait; " +
            $"if (Test-Path '{exePath}') {{ Start-Process '{exePath}' }}";

        Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
