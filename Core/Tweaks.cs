using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Velocity.Core;

public enum Risk { Safe, Moderate, Advanced }
public enum RebootNeed { None, SignOut, Restart }

/// <summary>A single desired registry value.</summary>
public sealed record RegChange(string Path, string Name, RegistryValueKind Kind, object Desired);

public abstract class Tweak
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public Risk Risk { get; init; } = Risk.Safe;
    public RebootNeed Reboot { get; init; } = RebootNeed.None;

    public abstract bool IsApplied();
    public abstract void Apply(BackupStore store);
    public abstract void Revert(BackupStore store);

    // ---------- shared registry helpers ----------

    protected static (bool existed, object? value) ReadValue(string path, string name)
    {
        var value = Registry.GetValue(path, name, null);
        if (value is not null) return (true, value);

        // GetValue returns null both for "no value" and "no key"; either way it doesn't exist.
        return (false, null);
    }

    protected static void CaptureOriginal(TweakBackup backup, string path, string name, RegistryValueKind kind)
    {
        if (backup.Registry.Any(e => e.Path == path && e.Name == name)) return; // original-first

        var (existed, value) = ReadValue(path, name);
        backup.Registry.Add(new RegistryBackupEntry
        {
            Path = path,
            Name = name,
            Kind = existed ? DetectKind(value!, kind).ToString() : kind.ToString(),
            Existed = existed,
            Value = existed ? SerializeValue(value!) : null,
        });
    }

    protected static void RestoreEntries(IEnumerable<RegistryBackupEntry> entries)
    {
        foreach (var e in entries)
        {
            if (e.Existed)
            {
                var kind = Enum.Parse<RegistryValueKind>(e.Kind);
                Registry.SetValue(e.Path, e.Name, DeserializeValue(e.Value ?? "", kind), kind);
            }
            else
            {
                DeleteValue(e.Path, e.Name);
            }
        }
    }

    protected static void DeleteValue(string fullPath, string name)
    {
        var (hive, subPath) = SplitHive(fullPath);
        using var key = hive.OpenSubKey(subPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    protected static (RegistryKey hive, string subPath) SplitHive(string fullPath)
    {
        var idx = fullPath.IndexOf('\\');
        var hiveName = fullPath[..idx];
        var sub = fullPath[(idx + 1)..];
        RegistryKey hive = hiveName switch
        {
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKEY_USERS" => Registry.Users,
            _ => throw new ArgumentException($"Unknown hive in {fullPath}"),
        };
        return (hive, sub);
    }

    private static RegistryValueKind DetectKind(object value, RegistryValueKind fallback) => value switch
    {
        int => RegistryValueKind.DWord,
        long => RegistryValueKind.QWord,
        string => RegistryValueKind.String,
        string[] => RegistryValueKind.MultiString,
        byte[] => RegistryValueKind.Binary,
        _ => fallback,
    };

    private static string SerializeValue(object value) => value switch
    {
        int i => unchecked((uint)i).ToString(),
        long l => unchecked((ulong)l).ToString(),
        string s => s,
        string[] a => string.Join('\n', a),
        byte[] b => Convert.ToBase64String(b),
        _ => value.ToString() ?? "",
    };

    private static object DeserializeValue(string s, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => unchecked((int)uint.Parse(s)),
        RegistryValueKind.QWord => unchecked((long)ulong.Parse(s)),
        RegistryValueKind.MultiString => s.Length == 0 ? Array.Empty<string>() : s.Split('\n'),
        RegistryValueKind.Binary => Convert.FromBase64String(s),
        _ => s,
    };

    protected static bool ValueMatches(string path, string name, object desired)
    {
        var (existed, current) = ReadValue(path, name);
        if (!existed) return false;
        return (current, desired) switch
        {
            (int a, int b) => a == b,
            (string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
            (long a, long b) => a == b,
            _ => Equals(current, desired),
        };
    }

    // ---------- process helper ----------

    protected static (int exitCode, string output) Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        return (p.HasExited ? p.ExitCode : -1, output);
    }
}

/// <summary>A tweak that is purely a fixed set of registry values.</summary>
public class RegistryTweak : Tweak
{
    public required IReadOnlyList<RegChange> Changes { get; init; }

    protected virtual IReadOnlyList<RegChange> ResolveChanges() => Changes;

    public override bool IsApplied()
    {
        var changes = ResolveChanges();
        return changes.Count > 0 && changes.All(c => ValueMatches(c.Path, c.Name, c.Desired));
    }

    public override void Apply(BackupStore store)
    {
        var backup = store.GetOrCreate(Id);
        foreach (var c in ResolveChanges())
        {
            CaptureOriginal(backup, c.Path, c.Name, c.Kind);
            Registry.SetValue(c.Path, c.Name, c.Desired, c.Kind);
        }
        store.Save();
    }

    public override void Revert(BackupStore store)
    {
        if (store.Tweaks.TryGetValue(Id, out var backup))
            RestoreEntries(backup.Registry);
        store.Remove(Id);
        store.Save();
    }
}

/// <summary>
/// Disables Nagle's algorithm on every network interface that currently has an
/// IP address. The interface list is resolved at apply time.
/// </summary>
public sealed class NagleTweak : RegistryTweak
{
    private const string InterfacesPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    protected override IReadOnlyList<RegChange> ResolveChanges()
    {
        var result = new List<RegChange>();
        using var ifaces = Registry.LocalMachine.OpenSubKey(InterfacesPath);
        if (ifaces is null) return result;

        foreach (var sub in ifaces.GetSubKeyNames())
        {
            using var k = ifaces.OpenSubKey(sub);
            if (k is null) continue;

            var dhcp = k.GetValue("DhcpIPAddress") as string;
            var stat = k.GetValue("IPAddress") as string[];
            bool hasIp = (dhcp is { Length: > 6 } && dhcp != "0.0.0.0")
                      || (stat?.Any(s => s.Length > 6 && s != "0.0.0.0") ?? false);
            if (!hasIp) continue;

            var path = $@"HKEY_LOCAL_MACHINE\{InterfacesPath}\{sub}";
            result.Add(new RegChange(path, "TcpAckFrequency", RegistryValueKind.DWord, 1));
            result.Add(new RegChange(path, "TCPNoDelay", RegistryValueKind.DWord, 1));
        }
        return result;
    }
}

/// <summary>
/// Activates the best available high-performance power plan. Prefers the hidden
/// "Ultimate Performance" plan; if this machine's Windows edition/hardware does
/// not offer it (common on laptops, VMs and some SKUs), it falls back to the
/// always-present built-in "High performance" plan. Remembers the previously
/// active plan (and any plan it had to create) so the change can be undone.
/// </summary>
public sealed class PowerPlanTweak : Tweak
{
    private const string UltimateGuid = "e9a42b02-d5df-448d-aa66-1f88b8f60c53";
    // Built-in "High performance" scheme GUID — present on every Windows install.
    private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static readonly Regex GuidRx = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    public static string? GetActiveScheme(out string? friendlyName)
    {
        friendlyName = null;
        var (code, output) = Run("powercfg", "/getactivescheme");
        if (code != 0) return null;
        var m = GuidRx.Match(output);
        var nameMatch = Regex.Match(output, @"\(([^)]+)\)");
        if (nameMatch.Success) friendlyName = nameMatch.Groups[1].Value;
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    public override bool IsApplied()
    {
        var active = GetActiveScheme(out var name);
        if (active is null) return false;
        if (active == UltimateGuid || active == HighPerfGuid) return true;
        if (name is not null &&
            (name.Contains("ultimate", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("high performance", StringComparison.OrdinalIgnoreCase)))
            return true;

        var store = BackupStore.Load();
        return store.Tweaks.TryGetValue(Id, out var b)
            && b.Data.TryGetValue("activatedScheme", out var activated)
            && activated == active;
    }

    /// <summary>Returns the GUID of an existing scheme whose name contains <paramref name="namePart"/>, or null.</summary>
    private static string? FindSchemeByName(string namePart)
    {
        var (code, output) = Run("powercfg", "/L");
        if (code != 0) return null;
        foreach (var line in output.Split('\n'))
        {
            var paren = Regex.Match(line, @"\(([^)]+)\)");
            if (paren.Success && paren.Groups[1].Value.Contains(namePart, StringComparison.OrdinalIgnoreCase))
            {
                var g = GuidRx.Match(line);
                if (g.Success) return g.Value.ToLowerInvariant();
            }
        }
        return null;
    }

    public override void Apply(BackupStore store)
    {
        var previous = GetActiveScheme(out _) ?? throw new InvalidOperationException("Could not read the active power plan.");
        var backup = store.GetOrCreate(Id);
        if (!backup.Data.ContainsKey("previousScheme"))
            backup.Data["previousScheme"] = previous;

        // 1. Ultimate Performance if the template GUID is directly activatable.
        if (Run("powercfg", $"/setactive {UltimateGuid}").exitCode == 0)
        {
            backup.Data["activatedScheme"] = UltimateGuid;
        }
        // 2. An Ultimate Performance plan that already exists under another GUID
        //    (Windows stores it this way once the hidden template has been unlocked).
        else if (FindSchemeByName("Ultimate Performance") is { } existingUltimate &&
                 Run("powercfg", $"/setactive {existingUltimate}").exitCode == 0)
        {
            backup.Data["activatedScheme"] = existingUltimate; // pre-existing; not ours to delete
        }
        else
        {
            // 3. Try to create Ultimate Performance from its hidden template.
            var (dupCode, dupOut) = Run("powercfg", $"-duplicatescheme {UltimateGuid}");
            var m = GuidRx.Match(dupOut);
            if (dupCode == 0 && m.Success)
            {
                var created = m.Value.ToLowerInvariant();
                backup.Data["createdScheme"] = created;   // deletable on revert (we made it)
                backup.Data["activatedScheme"] = created;
                Run("powercfg", $"/setactive {created}");
            }
            // 4. Fall back to the built-in High performance plan, which always exists.
            else if (Run("powercfg", $"/setactive {HighPerfGuid}").exitCode == 0)
            {
                backup.Data["activatedScheme"] = HighPerfGuid;
            }
            else if (FindSchemeByName("High performance") is { } existingHigh &&
                     Run("powercfg", $"/setactive {existingHigh}").exitCode == 0)
            {
                backup.Data["activatedScheme"] = existingHigh;
            }
            else
            {
                throw new InvalidOperationException(
                    "Could not activate a high-performance power plan on this system.\n\n" +
                    "This can happen on some laptops or virtual machines where the OEM " +
                    "restricts power plans.");
            }
        }
        store.Save();
    }

    public override void Revert(BackupStore store)
    {
        if (store.Tweaks.TryGetValue(Id, out var backup))
        {
            if (backup.Data.TryGetValue("previousScheme", out var prev))
                Run("powercfg", $"/setactive {prev}");
            // Only delete a scheme we actually created; never delete a built-in plan.
            if (backup.Data.TryGetValue("createdScheme", out var created))
                Run("powercfg", $"-delete {created}");
        }
        store.Remove(Id);
        store.Save();
    }
}

/// <summary>
/// Disables a Windows service (used for SysMain/Superfetch), remembering its
/// previous start type. The service is stopped immediately and restored —
/// including being started again if it was automatic — on revert.
/// </summary>
public sealed class ServiceTweak : Tweak
{
    public required string ServiceName { get; init; }

    private string StartValuePath => $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{ServiceName}";

    private int? CurrentStartType()
    {
        var (existed, value) = ReadValue(StartValuePath, "Start");
        return existed && value is int i ? i : null;
    }

    public override bool IsApplied() => CurrentStartType() == 4; // 4 = disabled

    public override void Apply(BackupStore store)
    {
        var current = CurrentStartType() ?? throw new InvalidOperationException($"Service '{ServiceName}' was not found.");
        var backup = store.GetOrCreate(Id);
        if (!backup.Data.ContainsKey("startType"))
            backup.Data["startType"] = current.ToString();

        var (code, output) = Run("sc.exe", $"config {ServiceName} start= disabled");
        if (code != 0) throw new InvalidOperationException($"Could not disable {ServiceName}:\n{output.Trim()}");
        Run("sc.exe", $"stop {ServiceName}"); // best effort; may already be stopped
        store.Save();
    }

    public override void Revert(BackupStore store)
    {
        if (store.Tweaks.TryGetValue(Id, out var backup) &&
            backup.Data.TryGetValue("startType", out var s) && int.TryParse(s, out var startType))
        {
            var mode = startType switch { 2 => "auto", 3 => "demand", 4 => "disabled", _ => "demand" };
            Run("sc.exe", $"config {ServiceName} start= {mode}");
            if (startType == 2)
                Run("sc.exe", $"start {ServiceName}");
        }
        store.Remove(Id);
        store.Save();
    }
}
