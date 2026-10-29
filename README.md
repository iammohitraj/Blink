# Blink

<img src="Assets/blink-logo.png" alt="Blink" width="128">

**Blink** hides and shows your Windows desktop icons instantly. Double-click empty
desktop space, press `Ctrl + Alt + D`, or click the tray icon — the icons are gone
(and back) in a blink.

Blink is a small tray utility with a dark themed installer. It installs per-user,
needs no administrator rights and keeps itself up to date from GitHub Releases.

## Install

1. Download **`BlinkSetup-<version>.exe`** from the
   [latest release](https://github.com/iammohitraj/Blink/releases/latest).
2. Run it and press **Install Blink**.

Setup copies Blink to `%LOCALAPPDATA%\Blink`, adds a Start Menu shortcut, registers
Blink in **Settings → Apps → Installed apps** and starts it in the notification area.
The `Start with Windows`, `Desktop shortcut` and `Launch after installing` options can
be changed in the installer before you install.

Blink runs with the same rights as you do — nothing is written outside your own
profile and no elevation prompt appears.

### Installing without the wizard

`BlinkSetup.exe --silent` performs the same install with no window, which is handy for
scripted setups. It is also how Blink's own updater applies a new version.

## Features

- **Instant toggle** — `Ctrl + Alt + D`, a double-click on empty desktop space, or the tray icon.
- **Dark themed installer and settings** — custom drawn to match Windows 11, with rounded corners.
- **Tray menu** — show/hide icons, settings, startup, updates and exit.
- **Automatic updates** — Blink checks GitHub Releases in the background and installs new versions in place.
- **Runs quietly** — no window, no service, no telemetry.
- **Self-contained** — nothing to install first; no .NET runtime needed.
- **Per-user** — everything lives in your own profile, so no admin rights and no UAC prompt.

## How to use

| Action | Result |
|---|---|
| `Ctrl + Alt + D` | Toggle desktop icons |
| Double-click empty desktop wallpaper | Toggle desktop icons |
| Double-click the tray icon | Toggle desktop icons |
| Right-click the tray icon | Menu: show/hide icons, settings, startup, updates, exit |

The settings window lets you turn the keyboard shortcut and the double-click
gesture on or off individually, choose whether Blink starts with Windows, and shows
the current state of the desktop icons.

## Updates

Blink checks for a newer release on startup (at most once every 12 hours) and then
in the background. When one is found:

* the tray menu entry changes to **Install update…**, and
* **Settings → Updates** shows the new version next to a one-click install button.

Installing an update downloads the new `BlinkSetup` into `%TEMP%\Blink-Update`,
runs it silently, and reopens Blink. Your settings are never touched by an update.
Automatic checking can be switched off in **Settings → Updates**.

You can also check manually at any time from the tray menu or the settings window.

## Command line

| Switch | Behaviour |
|---|---|
| *(none)* | Start Blink in the notification area |
| `--settings` | Start Blink and open the settings window (signals the running copy if one exists) |
| `--toggle` | Hide or show the desktop icons once, then exit — handy for shortcuts and scripts |
| `--uninstall` | Uninstall Blink after a confirmation prompt |
| `--uninstall --quiet` | Uninstall Blink without any prompt |

Only one copy of Blink runs at a time; starting a second one brings up the
settings window of the first instead of launching a duplicate.

## Where Blink stores things

| Path | Contents |
|---|---|
| `%LOCALAPPDATA%\Blink\Blink.exe` | The installed program |
| `%APPDATA%\Blink\settings.json` | Your preferences |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `Blink` | Start-with-Windows entry |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Blink` | The "Installed apps" entry |

## Uninstall

Open **Settings → Apps → Installed apps**, find **Blink** and choose *Uninstall*.
Blink asks for confirmation, then removes its shortcuts, its startup entry, its
settings and the program folder.

From a terminal:

```powershell
& "$env:LOCALAPPDATA\Blink\Blink.exe" --uninstall
```

## Requirements

* Windows 10 or Windows 11 (x64)
* No administrator rights
* No .NET runtime required

## Versioning and releases

The version lives in exactly one place:

```xml
<!-- Directory.Build.props -->
<Version>1.0.0</Version>
```

Both `Blink.exe` and `BlinkSetup.exe` inherit it, and `BlinkSetup.exe` reports it
to *Installed apps* as `DisplayVersion`. The in-app updater compares the newest
release tag against it, so **bumping this number is what cuts a release**.

To ship a new version:

```powershell
# Bump, build Blink.exe + BlinkSetup.exe, and write release\BlinkSetup-1.1.0.exe
powershell -ExecutionPolicy Bypass -File .\tools\Release.ps1 patch

# Same, but also commit the bump, tag v1.1.0, push and create the GitHub release
powershell -ExecutionPolicy Bypass -File .\tools\Release.ps1 patch -Publish
```

`patch`, `minor` and `major` step the version; `-Version 2.0.0` sets it explicitly
and `none` rebuilds the current one. Publish the asset under the exact name
`BlinkSetup-<version>.exe` inside a release tagged `v<version>` — that is the name
the updater looks for first. If no release exists yet, Blink simply reports that it
is up to date.

## Build from source

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
# The app (dist\Blink.exe)
dotnet publish Blink.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o dist

# The installer (Setup\dist-setup\BlinkSetup.exe) — run this after the app,
# because the installer embeds dist\Blink.exe at build time
dotnet publish Setup\BlinkSetup.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o Setup\dist-setup
```

The icon and brand logo are generated from source, so the artwork can be
regenerated at any time:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Generate-Icon.ps1
```

## Project layout

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point, single-instance handling, command line switches |
| `TrayApplicationContext.cs` | Tray icon, menu, global shortcut, double-click watcher, settings host |
| `AppInfo.cs` | Brand, version and release endpoint metadata |
| `AppSettings.cs` | Preference file handling |
| `DesktopIconController.cs` | Finds and shows/hides the desktop icon list view (`SHELLDLL_DefView` / `SysListView32`) |
| `DesktopHotKeyWindow.cs` | `Ctrl + Alt + D` registration |
| `DesktopDoubleClickWatcher.cs` | Detects double-clicks on empty desktop space |
| `StartupManager.cs` | Start-with-Windows registry entry |
| `Updates/` | GitHub Releases client, update coordinator and the uninstaller |
| `Ui/` | Dark theme: settings window, cards, buttons, switches, menu renderer, icon loading |
| `Setup/` | `BlinkSetup.exe`: install engine, installer window, progress bar |
| `tools/Release.ps1` | Version bump, build and release packaging |
| `tools/Generate-Icon.ps1` | Draws `Assets/blink.ico` and `Assets/blink-logo.png` |

