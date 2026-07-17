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

Requires Windows 10/11. No .NET runtime install needed — the published exe is self-contained.

```powershell
git clone <this repository's URL>
cd windows-pc-optimizer
.\install.ps1
```

This builds a release exe, then installs Velocity to `%LocalAppData%\Programs\Velocity` with Start Menu and Desktop shortcuts, plus a normal entry in **Settings > Apps** for uninstalling. No admin rights are needed for install itself — Velocity requests elevation only when you launch it, since registry, `powercfg`, and service changes require it.

To uninstall: use **Settings > Apps > Velocity**, or run `uninstall.ps1` from the install directory. Uninstalling does not revert any tweaks — use Velocity's own **Revert everything** first if you want your original Windows settings back.

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
