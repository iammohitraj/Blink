<div align="center">

<img src="src/Blink/Assets/blink-logo.png" alt="Blink" width="120">

# Blink

**Desktop icons, one blink away.**

[![Build](https://github.com/iammohitraj/Blink/actions/workflows/build.yml/badge.svg)](https://github.com/iammohitraj/Blink/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-7C5CFF.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-00C8FF.svg)](#requirements)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download)

</div>

Blink hides and shows your Windows desktop icons instantly. Double-click empty
desktop space, press `Ctrl + Alt + D`, or click the tray icon — the icons are gone
(and back) in a blink.

It is a small, self-contained tray app with a hand-drawn dark installer. No admin
rights, no service, no telemetry, and it keeps itself up to date from GitHub Releases.

## Install

1. Download **`BlinkSetup-<version>.exe`** from the
   [latest release](https://github.com/iammohitraj/Blink/releases/latest).
2. Run it and press **Install Blink**.

Setup copies Blink to `%LOCALAPPDATA%\Blink`, adds a Start Menu shortcut, registers
it under **Settings → Apps → Installed apps**, and starts it in the notification area.
You can change `Start with Windows`, `Desktop shortcut` and `Launch after installing`
before installing.

Blink runs with your own rights. Nothing is written outside your profile and no UAC
prompt appears.

<details>
<summary>Silent install</summary>

```powershell
.\BlinkSetup-1.1.0.exe --silent
```

The same install without a window, which is what Blink's updater uses.

</details>

## Features

- **Three ways to toggle** — global shortcut, double-click on empty wallpaper, or tray icon.
- **Instant** — a single `ShowWindow` call on the desktop's list view; nothing is rebuilt.
- **Dark, custom UI** — settings and installer are drawn from scratch to match Windows 11.
- **Automatic updates** — checks GitHub Releases in the background and updates in place.
- **Per-user and portable by design** — no admin rights, no service, no leftover drivers.
- **Self-contained** — ships with the .NET runtime, so there is nothing to install first.
- **Small and honest** — no telemetry, no background network traffic except the update check.

## Usage

| Action | Result |
|---|---|
| `Ctrl + Alt + D` | Toggle desktop icons |
| Double-click empty desktop wallpaper | Toggle desktop icons |
| Double-click the tray icon | Toggle desktop icons |
| Right-click the tray icon | Show/hide icons, settings, startup, updates, exit |

The settings window toggles the shortcut and the double-click gesture independently,
controls start-with-Windows, and shows the current icon state and update status.

### Command line

| Switch | Behaviour |
|---|---|
| *(none)* | Start Blink in the notification area |
| `--settings` | Start Blink and open settings (signals the running copy if there is one) |
| `--toggle` | Hide or show the icons once, then exit — handy for shortcuts and scripts |
| `--uninstall` | Uninstall after a confirmation prompt |
| `--uninstall --quiet` | Uninstall without a prompt |

Only one copy runs at a time; launching a second one brings up the first one's
settings window.

## Updates

Blink asks GitHub for the newest release at most once every 12 hours. When a newer
version exists, the tray menu shows **Install update…** and the settings window offers
a one-click install.

The updater downloads the installer to `%TEMP%\Blink-Update`, verifies the response,
runs it silently, and lets the installer replace `Blink.exe`. Preferences are never
touched. Automatic checks can be disabled in **Settings → Updates**.

## Where Blink stores things

| Path | Contents |
|---|---|
| `%LOCALAPPDATA%\Blink\Blink.exe` | The installed program |
| `%APPDATA%\Blink\settings.json` | Your preferences |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `Blink` | Start-with-Windows entry |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Blink` | The "Installed apps" entry |

## Uninstall

Use **Settings → Apps → Installed apps → Blink → Uninstall**, or run:

```powershell
& "$env:LOCALAPPDATA\Blink\Blink.exe" --uninstall
```

Blink removes its shortcuts, startup entry, settings and program folder.

## Requirements

- Windows 10 or Windows 11 (x64)
- No administrator rights
- No .NET runtime required (the release is self-contained)

## Repository layout

```text
Blink/
├─ src/
│  ├─ Blink/                  WinForms tray app, UI, updater
│  ├─ Blink.Core/             Data structures and algorithms library
│  └─ Blink.Setup/            Installer and uninstall engine
├─ tests/
│  └─ Blink.Core.Tests/       xUnit suite for Blink.Core
├─ benchmarks/
│  └─ Blink.Core.Benchmarks/  Allocation and timing harness
├─ docs/
│  ├─ ARCHITECTURE.md         How the app, updater and installer fit together
│  └─ ALGORITHMS.md           Every structure, operation and complexity
├─ tools/                     Icon generator and release script
├─ .github/                   CI, issue forms and PR template
├─ Blink.sln
└─ Directory.Build.props      Shared version and product metadata
```

### Blink.Core

The app's hot paths are backed by a small, dependency-free library written for this
project: a dynamic array, stacks, queues, a bounded LRU cache, an open-addressed hash
map, heaps, balanced trees, a trie, graph traversals, sorting and searching. Each
piece is tested and documented with its complexity.

It is used for real work inside Blink:

| Used by | Structure | Why |
|---|---|---|
| Update checks | `SemanticVersion` | Correct SemVer 2.0 ordering, prereleases included |
| Icon loading | `LruCache<int, Icon>` | Keeps the handful of sizes Windows asks for |
| Update download | `ArrayPool<byte>` | No per-download 80 KB buffer churn |
| Double-click detection | `PeriodicTimer` + thread-static buffers | No polling loop, no per-hit allocations |
| Settings listener | `WaitHandle.WaitAny` | Blocks until there is work instead of waking every 300 ms |

See [docs/ALGORITHMS.md](docs/ALGORITHMS.md) for the full reference.

## Build from source

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/iammohitraj/Blink.git
cd Blink

dotnet build Blink.sln -c Release
dotnet test  Blink.sln -c Release
```

Run the app straight from the build output:

```powershell
dotnet run --project src\Blink\Blink.csproj
```

### Package a release

```powershell
# Bump the version, test, build the app and installer, write release\BlinkSetup-<version>.exe
powershell -ExecutionPolicy Bypass -File .\tools\Release.ps1 patch
```

`patch`, `minor` and `major` step the version; `-Version 2.0.0` sets it explicitly;
`none` rebuilds the current one. Add `-Publish` to commit the bump, tag it, push and
create the GitHub release with the installer attached.

The installer embeds `dist\Blink.exe` at build time, so the script always publishes
the app before the setup project.

### Benchmarks

```powershell
dotnet run --project benchmarks\Blink.Core.Benchmarks -c Release
```

The harness prints wall-clock time and bytes allocated per benchmark, with checksums
so the JIT cannot skip work.

### Regenerate the artwork

The icon and logo are drawn with `System.Drawing`, so the brand assets live in source:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Generate-Icon.ps1
```

## Versioning

The version lives in exactly one place — `Directory.Build.props`. Blink, Blink Setup
and the release asset all inherit it, and the updater compares GitHub tags against it.
Tags are `v<major>.<minor>.<patch>` and the asset is always
`BlinkSetup-<version>.exe`.

## Contributing

Bug reports and pull requests are welcome. Please read
[CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) first.
Security issues should follow [SECURITY.md](SECURITY.md) instead of a public issue.

## License

[MIT](LICENSE) © 2026 Mohit Raj
