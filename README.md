<div align="center">

# ⚡ Velocity

### Gaming PC Optimizer for Windows

**Deeply tune your PC for gaming in one click — and undo every single change just as easily.**

[![Download](https://img.shields.io/github/v/release/takhoffman/windows-pc-optimizer?style=for-the-badge&label=Download&color=22D3EE)](https://github.com/takhoffman/windows-pc-optimizer/releases/latest)
&nbsp;
![platform](https://img.shields.io/badge/Windows%2010%20%2F%2011-0A0D14?style=for-the-badge&logo=windows&logoColor=22D3EE)
![.NET 9](https://img.shields.io/badge/.NET%209%20·%20WPF-8B5CF6?style=for-the-badge)
![license](https://img.shields.io/badge/MIT-1B2A4A?style=for-the-badge)

</div>

---

Velocity applies the registry, power, network, and scheduling tweaks that competitive players and tuning guides reach for — but wraps them in a fast, native app that **snapshots the original value of everything it touches** so any change is a single click away from being reverted. No subscriptions, no telemetry, no background bloat.

## Why Velocity

- **🔄 Everything is revertible.** Before touching any setting, Velocity saves its original value to `%ProgramData%\Velocity\backup.json`. Revert one tweak, or all of them, whenever you want. Re-applying never overwrites the stored original, so "revert" always restores your true pre-Velocity state.
- **🪶 Zero background footprint.** No service, no tray agent, no startup entry. Velocity applies your settings and closes — it uses **nothing** while you're actually gaming.
- **🎯 Game-aware profiles.** Curated tweak bundles for how you actually play, with automatic detection of games installed via Steam, Riot, and Epic. Games you own light up in the UI.
- **⚙️ Native, not Electron.** WPF on .NET 9 as a single self-contained `.exe` with no third-party dependencies. Fast cold start, tiny memory footprint, no runtime to install.
- **🛡️ Honest about risk.** Every tweak is labeled Safe / Moderate / Advanced, tells you if it needs a restart, and advanced tweaks always ask before applying.

## Download & install

**The easy way** — grab the installer from the [**latest release**](https://github.com/takhoffman/windows-pc-optimizer/releases/latest):

1. Download `VelocitySetup-x.y.z.msi`.
2. Double-click it and follow the wizard (Windows will prompt for admin — the app installs to Program Files and needs elevation to change system settings).
3. Launch from the Start Menu or Desktop shortcut.

It installs like any normal Windows program and appears in **Settings → Apps**, so you can uninstall it cleanly from there at any time.

> **Note:** Launching Velocity does nothing to your system on its own. Settings only change when you flip a switch or apply a profile — and every change is logged in the Backup & Restore tab, ready to undo.

## What it tunes

| Category | Tweaks |
|---|---|
| **Windows Gaming** | Game Mode · disable Xbox Game DVR / background capture |
| **GPU & Display** | Fullscreen-exclusive latency path · hardware-accelerated GPU scheduling |
| **CPU & Scheduling** | Multimedia responsiveness · game scheduler priority · max-performance power plan |
| **Network & Latency** | Disable network throttling · disable Nagle's algorithm (per active NIC) |
| **Input** | Disable mouse pointer acceleration (1:1 raw aim) |
| **System & Services** | Best-performance visual effects · remove startup delay · restrict background Store apps · disable SysMain *(advanced)* |

### Game profiles

Apply a whole bundle at once, tuned for a play style:

- **🎮 Competitive FPS** — everything focused on the lowest input and network latency (Valorant, CS2, Apex, Overwatch 2, Fortnite).
- **👥 MOBA & Esports** — stable frame times and low ping for team play (League of Legends, Dota 2, Rocket League).
- **🖥️ AAA Single-Player** — maximum throughput and smoothness for demanding open worlds (Cyberpunk 2077, Elden Ring, Baldur's Gate 3, Starfield…).
- **⚡ Max Performance** — every optimization Velocity has, applied at once. Still fully revertible.

## How reverting works

Velocity keeps a single JSON backup at `%ProgramData%\Velocity\backup.json`:

- The **first** time a tweak is applied, the original registry values / power-plan / service state are recorded. Subsequent re-applies never overwrite that snapshot.
- **Revert** restores exactly what was captured — deleting values that didn't exist before, and restoring the ones that did.
- Power-plan reverting only deletes plans Velocity created; it never removes a built-in Windows plan.
- If the backup file is ever corrupted, it's preserved as a `.corrupt-*` copy rather than silently discarded.

Uninstalling Velocity does **not** revert your tweaks — use **Revert everything** in the app first if you want your original Windows settings back before removing it.

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

```powershell
git clone https://github.com/takhoffman/windows-pc-optimizer.git
cd windows-pc-optimizer

# Run it directly:
dotnet build -c Release
.\bin\Release\net9.0-windows\Velocity.exe
```

### Build the MSI installer

```powershell
.\tools\build-installer.ps1
```

This publishes a self-contained single-file exe and produces `Setup\dist\VelocitySetup.msi`. The script is self-restoring — it installs the pinned [WiX Toolset](https://wixtoolset.org/) v5 CLI and required extensions automatically if they're missing.

> WiX is pinned to **v5** on purpose: v7+ requires accepting a paid Open Source Maintenance Fee EULA before its CLI will run. v5 has no such gate.

### Portable per-user install (no admin needed to install)

```powershell
.\install.ps1
```

Installs to `%LocalAppData%\Programs\Velocity` with its own shortcuts and an uninstall entry, without needing admin rights for the install step itself (the app still elevates when launched).

## Tech notes

- **Stack:** C# / .NET 9 / WPF, MVVM, no external NuGet dependencies.
- **Elevation:** the app manifest requests administrator rights, since registry (HKLM), `powercfg`, and service control all require it.
- **Diagnostics:** unhandled exceptions are logged to `%LocalAppData%\Velocity\error.log`.

## Disclaimer

Velocity changes real Windows system settings. While every change it makes is designed to be reverted from within the app, you use it at your own risk. Start with the Safe tweaks, and use **Revert everything** if anything feels off.

## License

[MIT](LICENSE) © Tak Hoffman
