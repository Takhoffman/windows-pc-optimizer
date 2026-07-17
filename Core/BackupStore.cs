using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Velocity.Core;

/// <summary>
/// One saved registry value (or its absence) captured before a tweak modified it.
/// </summary>
public sealed class RegistryBackupEntry
{
    public string Path { get; set; } = "";      // full path incl. hive, e.g. HKEY_CURRENT_USER\Software\...
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "DWord"; // DWord | QWord | String | MultiString
    public bool Existed { get; set; }
    public string? Value { get; set; }          // serialized; MultiString joined with \n
}

public sealed class TweakBackup
{
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public List<RegistryBackupEntry> Registry { get; set; } = new();
    public Dictionary<string, string> Data { get; set; } = new(); // for non-registry state (power scheme GUIDs, service start type)
}

/// <summary>
/// Persists the pre-tweak state of the machine so every change can be undone.
/// Original-first semantics: once a value is captured for a tweak it is never
/// overwritten by later re-applies, so "revert" always restores the true original.
/// </summary>
public sealed class BackupStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Velocity");

    public static string FilePath => Path.Combine(Directory, "backup.json");

    public Dictionary<string, TweakBackup> Tweaks { get; set; } = new();

    public static BackupStore Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<BackupStore>(File.ReadAllText(FilePath), JsonOpts) ?? new BackupStore();
        }
        catch
        {
            // Corrupt store: keep the broken file aside instead of silently discarding it.
            try { File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), overwrite: true); } catch { }
        }
        return new BackupStore();
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
    }

    public TweakBackup GetOrCreate(string tweakId)
    {
        if (!Tweaks.TryGetValue(tweakId, out var b))
            Tweaks[tweakId] = b = new TweakBackup();
        return b;
    }

    public bool Has(string tweakId) => Tweaks.ContainsKey(tweakId);

    public void Remove(string tweakId) => Tweaks.Remove(tweakId);
}
