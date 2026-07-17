using Microsoft.Win32;

namespace Velocity.Core;

public sealed class GameRef
{
    public required string Name { get; init; }
    /// <summary>Folder name under a Steam library's steamapps\common, if the game ships on Steam.</summary>
    public string? SteamFolder { get; init; }
    /// <summary>Absolute paths where non-Steam launchers install the game by default.</summary>
    public string[] KnownPaths { get; init; } = Array.Empty<string>();
}

public sealed class GameProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Tagline { get; init; }
    public required string IconGlyph { get; init; }
    public required IReadOnlyList<string> TweakIds { get; init; }
    public required IReadOnlyList<GameRef> Games { get; init; }
}

public static class Catalog
{
    private const string GameConfigStore = @"HKEY_CURRENT_USER\System\GameConfigStore";
    private const string SystemProfile = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    public static IReadOnlyList<Tweak> Tweaks { get; } = new List<Tweak>
    {
        // ---------------- Windows Gaming ----------------
        new RegistryTweak
        {
            Id = "game-mode",
            Name = "Windows Game Mode",
            Description = "Lets Windows prioritize your game and suppress driver installs and update restarts while playing.",
            Category = "Windows Gaming",
            Changes = new[]
            {
                new RegChange(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", RegistryValueKind.DWord, 1),
                new RegChange(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", RegistryValueKind.DWord, 1),
            },
        },
        new RegistryTweak
        {
            Id = "disable-game-dvr",
            Name = "Disable Xbox Game DVR",
            Description = "Stops background gameplay recording and capture, which costs FPS even when you never use it.",
            Category = "Windows Gaming",
            Changes = new[]
            {
                new RegChange(GameConfigStore, "GameDVR_Enabled", RegistryValueKind.DWord, 0),
                new RegChange(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValueKind.DWord, 0),
            },
        },

        // ---------------- GPU & Display ----------------
        new RegistryTweak
        {
            Id = "fse-optimizations",
            Name = "Optimize fullscreen behavior",
            Description = "Tunes fullscreen-exclusive handling so games get the lowest-latency presentation path.",
            Category = "GPU & Display",
            Changes = new[]
            {
                new RegChange(GameConfigStore, "GameDVR_FSEBehaviorMode", RegistryValueKind.DWord, 2),
                new RegChange(GameConfigStore, "GameDVR_HonorUserFSEBehaviorMode", RegistryValueKind.DWord, 1),
                new RegChange(GameConfigStore, "GameDVR_DXGIHonorFSEWindowsCompatible", RegistryValueKind.DWord, 1),
            },
        },
        new RegistryTweak
        {
            Id = "hags",
            Name = "Hardware-accelerated GPU scheduling",
            Description = "Lets the GPU manage its own scheduling, reducing latency on modern NVIDIA/AMD cards.",
            Category = "GPU & Display",
            Risk = Risk.Moderate,
            Reboot = RebootNeed.Restart,
            Changes = new[]
            {
                new RegChange(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", RegistryValueKind.DWord, 2),
            },
        },

        // ---------------- CPU & Scheduling ----------------
        new RegistryTweak
        {
            Id = "sys-responsiveness",
            Name = "Prioritize games over background tasks",
            Description = "Sets multimedia system responsiveness to 0 so games are never throttled in favor of background work.",
            Category = "CPU & Scheduling",
            Changes = new[]
            {
                new RegChange(SystemProfile, "SystemResponsiveness", RegistryValueKind.DWord, 0),
            },
        },
        new RegistryTweak
        {
            Id = "games-task-priority",
            Name = "Raise game scheduling priority",
            Description = "Gives the 'Games' scheduler task high CPU, GPU and I/O priority.",
            Category = "CPU & Scheduling",
            Changes = new[]
            {
                new RegChange(SystemProfile + @"\Tasks\Games", "GPU Priority", RegistryValueKind.DWord, 8),
                new RegChange(SystemProfile + @"\Tasks\Games", "Priority", RegistryValueKind.DWord, 6),
                new RegChange(SystemProfile + @"\Tasks\Games", "Scheduling Category", RegistryValueKind.String, "High"),
                new RegChange(SystemProfile + @"\Tasks\Games", "SFIO Priority", RegistryValueKind.String, "High"),
            },
        },
        new PowerPlanTweak
        {
            Id = "ultimate-power",
            Name = "Ultimate Performance power plan",
            Description = "Activates Windows' hidden Ultimate Performance plan: no core parking, no micro-latency from power state switching.",
            Category = "CPU & Scheduling",
        },

        // ---------------- Network & Latency ----------------
        new RegistryTweak
        {
            Id = "network-throttling",
            Name = "Disable network throttling",
            Description = "Removes the packet-per-millisecond cap Windows applies while multimedia apps run.",
            Category = "Network & Latency",
            Changes = new[]
            {
                new RegChange(SystemProfile, "NetworkThrottlingIndex", RegistryValueKind.DWord, unchecked((int)0xFFFFFFFF)),
            },
        },
        new NagleTweak
        {
            Id = "nagle",
            Name = "Disable Nagle's algorithm",
            Description = "Sends small packets immediately instead of batching them — lower ping in many online games.",
            Category = "Network & Latency",
            Risk = Risk.Moderate,
            Reboot = RebootNeed.Restart,
            Changes = Array.Empty<RegChange>(), // resolved per network interface at runtime
        },

        // ---------------- Input ----------------
        new RegistryTweak
        {
            Id = "mouse-precision",
            Name = "Disable pointer acceleration",
            Description = "Turns off 'Enhance pointer precision' for 1:1 raw mouse aim, the standard for FPS play.",
            Category = "Input",
            Changes = new[]
            {
                new RegChange(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", RegistryValueKind.String, "0"),
                new RegChange(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", RegistryValueKind.String, "0"),
                new RegChange(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", RegistryValueKind.String, "0"),
            },
        },

        // ---------------- System & Services ----------------
        new RegistryTweak
        {
            Id = "visual-fx",
            Name = "Visual effects: best performance",
            Description = "Switches Windows animations and transparency to the 'best performance' preset.",
            Category = "System & Services",
            Risk = Risk.Moderate,
            Reboot = RebootNeed.SignOut,
            Changes = new[]
            {
                new RegChange(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", RegistryValueKind.DWord, 2),
            },
        },
        new RegistryTweak
        {
            Id = "startup-delay",
            Name = "Remove startup app delay",
            Description = "Removes the artificial delay Windows adds before launching startup apps.",
            Category = "System & Services",
            Reboot = RebootNeed.SignOut,
            Changes = new[]
            {
                new RegChange(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", RegistryValueKind.DWord, 0),
            },
        },
        new RegistryTweak
        {
            Id = "background-apps",
            Name = "Restrict background Store apps",
            Description = "Stops Microsoft Store apps from running in the background while you play.",
            Category = "System & Services",
            Risk = Risk.Moderate,
            Changes = new[]
            {
                new RegChange(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", RegistryValueKind.DWord, 1),
            },
        },
        new ServiceTweak
        {
            Id = "sysmain",
            Name = "Disable SysMain (Superfetch)",
            Description = "Stops the prefetch service. Can reduce stutter on SSD systems; leave on if you use a hard drive.",
            Category = "System & Services",
            Risk = Risk.Advanced,
            ServiceName = "SysMain",
        },
    };

    public static Tweak? Find(string id) => Tweaks.FirstOrDefault(t => t.Id == id);

    public static IReadOnlyList<GameProfile> Profiles { get; } = new List<GameProfile>
    {
        new GameProfile
        {
            Id = "competitive-fps",
            Name = "Competitive FPS",
            Tagline = "Every millisecond of input and network latency stripped out.",
            IconGlyph = "", // game controller
            TweakIds = new[] { "game-mode", "disable-game-dvr", "fse-optimizations", "sys-responsiveness", "games-task-priority", "ultimate-power", "network-throttling", "nagle", "mouse-precision" },
            Games = new[]
            {
                new GameRef { Name = "VALORANT", KnownPaths = new[] { @"C:\Riot Games\VALORANT" } },
                new GameRef { Name = "Counter-Strike 2", SteamFolder = "Counter-Strike Global Offensive" },
                new GameRef { Name = "Apex Legends", SteamFolder = "Apex Legends" },
                new GameRef { Name = "Overwatch 2", KnownPaths = new[] { @"C:\Program Files (x86)\Overwatch" } },
                new GameRef { Name = "Fortnite", KnownPaths = new[] { @"C:\Program Files\Epic Games\Fortnite" } },
            },
        },
        new GameProfile
        {
            Id = "esports-moba",
            Name = "MOBA & Esports",
            Tagline = "Stable frame times and low ping for competitive team play.",
            IconGlyph = "", // people
            TweakIds = new[] { "game-mode", "disable-game-dvr", "sys-responsiveness", "games-task-priority", "ultimate-power", "network-throttling", "mouse-precision" },
            Games = new[]
            {
                new GameRef { Name = "League of Legends", KnownPaths = new[] { @"C:\Riot Games\League of Legends" } },
                new GameRef { Name = "Dota 2", SteamFolder = "dota 2 beta" },
                new GameRef { Name = "Rocket League", SteamFolder = "rocketleague", KnownPaths = new[] { @"C:\Program Files\Epic Games\rocketleague" } },
            },
        },
        new GameProfile
        {
            Id = "aaa-immersive",
            Name = "AAA Single-Player",
            Tagline = "Maximum throughput and smoothness for demanding open worlds.",
            IconGlyph = "", // display
            TweakIds = new[] { "game-mode", "disable-game-dvr", "hags", "sys-responsiveness", "games-task-priority", "ultimate-power" },
            Games = new[]
            {
                new GameRef { Name = "Cyberpunk 2077", SteamFolder = "Cyberpunk 2077" },
                new GameRef { Name = "Elden Ring", SteamFolder = "ELDEN RING" },
                new GameRef { Name = "Baldur's Gate 3", SteamFolder = "Baldurs Gate 3" },
                new GameRef { Name = "Red Dead Redemption 2", SteamFolder = "Red Dead Redemption 2" },
                new GameRef { Name = "Starfield", SteamFolder = "Starfield" },
            },
        },
        new GameProfile
        {
            Id = "max-performance",
            Name = "Max Performance",
            Tagline = "Everything Velocity has, applied at once. Fully revertible.",
            IconGlyph = "", // lightning
            TweakIds = Tweaks.Select(t => t.Id).ToArray(),
            Games = Array.Empty<GameRef>(),
        },
    };
}
