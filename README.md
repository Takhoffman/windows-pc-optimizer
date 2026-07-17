# Velocity — Gaming PC Optimizer

A native Windows app that deeply optimizes your PC for gaming — and can undo every single change it makes.

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0A0D14?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-9-22D3EE?style=flat-square)
![license](https://img.shields.io/badge/license-MIT-8B5CF6?style=flat-square)

## Why it's different

- **Everything is revertible.** Before Velocity touches any setting, it snapshots the original value to `%ProgramData%\Velocity\backup.json`. Revert one tweak or everything with one click, any time.
- **Zero background footprint.** No service, no tray agent, no startup entry. Velocity applies your settings and gets out of the way — it uses 0 resources while you're actually gaming.
- **Native, not Electron.** WPF on .NET 9, single self-contained exe, no third-party dependencies. Fast cold start, tiny memory use while open.
- **Game-aware profiles.** Curated tweak bundles for Competitive FPS, MOBA & Esports, and AAA single-player — with automatic detection of installed games (Steam, Riot, Epic).

## Tweaks included

| Category | Tweaks |
|---|---|
| Windows Gaming | Game Mode, disable Xbox Game DVR |
| GPU & Display | Fullscreen-exclusive optimizations, hardware-accelerated GPU scheduling |
| CPU & Scheduling | Multimedia responsiveness, game scheduler priority, Ultimate Performance power plan |
| Network & Latency | Disable network throttling, disable Nagle's algorithm |
| Input | Disable pointer acceleration |
| System & Services | Visual effects for performance, startup delay removal, background Store apps, SysMain/Superfetch (advanced) |

Every tweak is labeled **Safe / Moderate / Advanced**, and anything needing a restart or sign-out says so up front. Advanced tweaks always ask for confirmation before applying.

## Install

Requires Windows 10/11. No .NET runtime install needed — everything is self-contained.

### Windows installer (recommended)

```powershell
git clone <this repository's URL>
cd windows-pc-optimizer
.\tools\build-installer.ps1
```

This publishes the app and produces `Setup\dist\VelocitySetup.msi` — a normal Windows installer with a license/install-directory wizard, a Start Menu entry, a Desktop shortcut, and a "Launch Velocity now" option on the last page. Double-click the MSI to install (it will prompt for admin rights, since it installs to Program Files and the app itself always needs elevation to run). It shows up in **Settings > Apps** like any other program, and uninstalling from there removes everything cleanly.

Building the MSI yourself requires the [WiX Toolset](https://wixtoolset.org/) v5 CLI (`dotnet tool install --global wix --version 5.0.2`, plus the `WixToolset.UI.wixext` and `WixToolset.Util.wixext` extensions at the same version) — WiX v7+ requires accepting its paid Open Source Maintenance Fee EULA, so this project pins to v5, which doesn't.

### Portable per-user install (no MSI/admin needed to install)

```powershell
.\install.ps1
```

Installs to `%LocalAppData%\Programs\Velocity` with its own Start Menu/Desktop shortcuts and an **Settings > Apps** uninstall entry, without needing admin rights for the install step itself (Velocity still elevates when it launches). Run `uninstall.ps1` from the install directory, or use Settings > Apps, to remove it.

Either way, uninstalling does not revert any tweaks — use Velocity's own **Revert everything** first if you want your original Windows settings back.

### Build from source without installing

```powershell
dotnet build -c Release
.\bin\Release\net9.0-windows\Velocity.exe
```

## Safety notes

- Backups use original-first semantics: re-applying a tweak never overwrites the stored original, so "revert" always restores the true pre-Velocity state.
- If `backup.json` ever fails to parse, it's preserved as a `.corrupt-*` copy rather than discarded.
- SysMain disabling is only recommended on SSD systems and is marked Advanced.

## License

MIT
