using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Velocity.Core;

/// <summary>
/// Best-effort detection of installed games: scans Steam library folders and a
/// few well-known launcher install paths. Failures are silently ignored — the
/// result only drives UI highlighting.
/// </summary>
public static class GameDetector
{
    private static readonly Lazy<List<string>> SteamCommonDirs = new(FindSteamCommonDirs);

    public static bool IsInstalled(GameRef game)
    {
        try
        {
            if (game.SteamFolder is not null &&
                SteamCommonDirs.Value.Any(dir => Directory.Exists(Path.Combine(dir, game.SteamFolder))))
                return true;

            return game.KnownPaths.Any(Directory.Exists);
        }
        catch
        {
            return false;
        }
    }

    private static List<string> FindSteamCommonDirs()
    {
        var result = new List<string>();
        try
        {
            var installPath =
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string ??
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
            if (installPath is null) return result;

            var libraryRoots = new List<string> { installPath };
            var vdf = Path.Combine(installPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libraryRoots.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }

            foreach (var root in libraryRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var common = Path.Combine(root, "steamapps", "common");
                if (Directory.Exists(common)) result.Add(common);
            }
        }
        catch { }
        return result;
    }
}
