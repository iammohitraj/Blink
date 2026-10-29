# Architecture

Blink is one WinForms process, one installer and one clean separation between them.
This document describes how the pieces fit together and why the process is shaped the
way it is.

## Projects

```text
Blink.sln
├─ src/Blink.Core          net8.0         Data structures, algorithms, SemVer
├─ src/Blink               net8.0-windows Tray app and updater
├─ src/Blink.Setup         net8.0-windows Installer (embeds Blink.exe)
├─ tests/Blink.Core.Tests  net8.0         xUnit suite
└─ benchmarks/Blink.Core.Benchmarks      Simple timing/alloc harness
```

`Blink.Setup` links a few UI and brand files from `src/Blink` instead of copying them,
so the installer and the app cannot drift apart. Both projects take a project
reference on `Blink.Core`.

## Runtime shape

```text
┌──────────────────────────── Blink.exe ────────────────────────────┐
│                                                                   │
│  Program.Main                                                     │
│   ├─ --toggle / --uninstall → one-shot work, then exit            │
│   ├─ mutex (Local\Blink.SingleInstance) → single instance         │
│   └─ TrayApplicationContext                                       │
│        ├─ NotifyIcon + dark context menu                          │
│        ├─ DesktopHotKeyWindow   (message-only window, Ctrl+Alt+D) │
│        ├─ DesktopDoubleClickWatcher (PeriodicTimer, 35 ms)        │
│        ├─ UpdateCoordinator    (background check + download)      │
│        ├─ SettingsForm         (created on first open)            │
│        └─ AppSettings          (%APPDATA%\Blink\settings.json)    │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

### Threading

- **UI thread** owns every control, the tray icon, settings writes and all state
  transitions. `UpdateCoordinator` publishes results through the captured
  `SynchronizationContext`.
- **Worker threads** exist for three narrow jobs: the update check, the installer
  download, and the double-click watcher. None of them touch UI state directly.
- **Shutdown** is cooperative: a `CancellationTokenSource` stops the watcher, and the
  settings listener waits on both the launch event and that token, so nothing polls.

### Icon toggle flow

1. A trigger fires: global hotkey, tray click, double-click on wallpaper, or `--toggle`.
2. `DesktopIconController` resolves the desktop list view:
   - `FindWindow("Progman")` → `SHELLDLL_DefView` → `SysListView32`.
   - If that fails, `EnumWindows` walks top-level windows looking for the same pair.
   - The handle is cached and revalidated with `IsWindow` before reuse.
3. `ShowWindow(listView, SW_HIDE | SW_SHOW)` hides or shows the icons.
4. `TrayApplicationContext` updates the menu, the tray tooltip and raises
   `StateChanged`, which the settings window listens to.

No Explorer restart, no registry change and no shell hook is involved.

### Double-click detection

Windows has no "double-clicked the wallpaper" message, so Blink samples the physical
left button with `GetAsyncKeyState` on a 35 ms `PeriodicTimer`. A click qualifies when
it is within the system double-click time and rectangle of the previous click, and
`WindowFromPoint` resolves to the desktop surface (or blank list-view space). The
toggle is delayed 120 ms so the second click's `WM_LBUTTONUP` never lands on a window
that is about to disappear.

### Update lifecycle

```text
startup ──► last check < 12 h? ──yes──► skip
                    │no
                    ▼
        GET /repos/iammohitraj/Blink/releases/latest
                    │
        ┌───────────┴────────────┐
        ▼                        ▼
  tag parses?              tag invalid / no release
        │                        └─► "No updates yet"
        ▼
  SemVer newer than AppInfo.ParsedVersion?
        │no                       │yes
        ▼                        ▼
   "Up to date"          download BlinkSetup to %TEMP%\Blink-Update
                                  │
                          run with --silent, then exit
```

The installer replaces the executable and restarts Blink, so the updater never has to
patch files in place. Settings survive because they live in `%APPDATA%`, not next to
the executable.

### Settings

`AppSettings` is a plain POCO serialized with `System.Text.Json` using one shared
`JsonSerializerOptions`. Writes go to a temp file and are swapped in with
`File.Move(..., overwrite: true)`, so a crash mid-write cannot truncate the file.
Corrupt or missing files fall back to defaults.

## Installer

`BlinkSetup.exe` embeds the published `Blink.exe` as a manifest resource named
`Blink.Setup.payload.exe`. `InstallService` is shared by the wizard and the silent
update path and performs the same steps either way:

1. Extract the payload into `%LOCALAPPDATA%\Blink`.
2. Write the uninstall entry under
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Blink`.
3. Optionally add the `Run` entry and a desktop shortcut.
4. Optionally launch Blink.

Everything is per-user; setup never requests elevation.

## Build and release

- `Directory.Build.props` is the single source of truth for the version and product
  metadata.
- `tools/Release.ps1` bumps the version, runs the tests, publishes the app, publishes
  the installer with the app embedded, and writes
  `release\BlinkSetup-<version>.exe`.
- `tools/Generate-Icon.ps1` draws the multi-resolution icon and logo with
  `System.Drawing`, so the artwork is reproducible from source.
- `.github/workflows/build.yml` restores, builds and tests every push and pull request.

## Error handling philosophy

Blink is a convenience utility, so it fails soft:

- A missing desktop list view shows a tray balloon instead of crashing.
- A failed settings write is ignored; defaults apply next launch.
- A failed update check only changes the status label.
- Registry and shortcut failures surface as a message, never an exception dialog.

The only hard requirements are a Windows desktop and a writable user profile.
