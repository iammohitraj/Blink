# Architecture

Blink is one WinForms process, one installer and one clean separation between them.
This document describes how the pieces fit together and why the process is shaped the
way it is.

## Projects

```text
Blink.sln
├─ src/Blink.Core          net8.0         Data structures, algorithms, SemVer
├─ src/Blink               net8.0-windows Tray app, wallpapers, updater
├─ src/Blink.Setup         net8.0-windows Installer (embeds Blink.exe)
├─ tests/Blink.Core.Tests  net8.0         xUnit suite for Blink.Core
├─ tests/Blink.Tests       net8.0-windows xUnit suite for wallpapers
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
│        ├─ DesktopHotKeyWindow  (Ctrl+Alt+D, Ctrl+Alt+W)           │
│        ├─ DesktopDoubleClickWatcher (PeriodicTimer, 35 ms)        │
│        ├─ WallpaperService     (library, Windows wallpaper)       │
│        │    └─ DesktopOverlayWindow (mini wallpaper, layered)     │
│        ├─ UpdateCoordinator    (background check + download)      │
│        ├─ SettingsForm         (created on first open)            │
│        ├─ WallpaperForm        (gallery + mini wallpaper)         │
│        └─ AppSettings          (%APPDATA%\Blink\settings.json)    │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

### Threading

- **UI thread** owns every control, the tray icon, settings writes and all state
  transitions. `UpdateCoordinator` publishes results through the captured
  `SynchronizationContext`. The mini wallpaper is rendered and animated there too, one
  coalesced frame at a time.
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

### Wallpapers

`WallpaperService` is the single entry point used by the tray menu, the shortcut and
the wallpaper window. It owns a `WallpaperLibrary` pointed at `%APPDATA%\Blink` and
keeps three pieces in sync: `AppSettings`, the Windows wallpaper and the mini overlay.

**Library.** Files are copied into `wallpapers\` with a sanitized, unique name
(`name (2).ext`), so the library survives the originals being moved. Imports are
validated by decoding them, keep only `jpg/jpeg/png/bmp/gif` up to 150 MB, and skip
files that are already inside the folder. `settings.json` stores only the file name,
never an absolute path. Thumbnails (256×144 JPEG) and GIF stills live in their own
folders and are regenerated when missing or older than the source.

**Applying.** `Apply` captures the pre-existing Windows wallpaper once, converts a GIF
to a cached first-frame PNG (Windows paints stills only), sets `WallpaperStyle=10`
(Fill) in `HKCU\Control Panel\Desktop`, and calls
`SystemParametersInfo(SPI_SETDESKWALLPAPER)`. The captured path, style and tile stay
in settings so **Restore previous** can put the original back exactly as it was.

**Mini wallpaper.** Windows' `WorkerW`/`0x052C` technique reparents a window under the
desktop, but it is undocumented and breaks when Explorer restarts. Blink instead keeps
`DesktopOverlayWindow` as an independent top-level window with
`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`:

1. `UpdateLayeredWindow` supplies a 32-bit premultiplied canvas with per-pixel alpha,
   so the image gets a rounded, antialiased edge without a shaped region.
2. `SetWindowPos(overlay, desktopRoot, SWP_NOMOVE | SWP_NOSIZE)` places it directly
   above `Progman` (or the `WorkerW` hosting `SHELLDLL_DefView`), which puts it below
   every normal window. The desktop icons live in that same host, so the overlay can
   overlap icons in its corner — they stay clickable because `WS_EX_TRANSPARENT` lets
   input through. The extended style set also keeps it out of Alt+Tab and the taskbar.
3. A 2-second `System.Windows.Forms.Timer` re-anchors it if the z-order changed and
   recomputes the geometry when the DPI or working area changes — the recovery path
   for an Explorer restart.
4. Animated GIFs are driven by `ImageAnimator`; frame events are coalesced through an
   `Interlocked` flag and a single `BeginInvoke`, so a fast GIF cannot flood the
   message queue.

Geometry lives in `MiniWallpaperLayout`, which has no window or GDI dependencies and is
covered by unit tests: `Measure` keeps the source aspect ratio within a sane band,
`Place` handles the four corners and DPI-scaled margin, and `Fit`/`Cover` are the
contain and cover rules used by the overlay and the thumbnails.

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
`JsonSerializerOptions`. Enums are written as strings, so new corner or size values stay
readable and old files keep loading. Writes go to a temp file and are swapped in with
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
- `tools/Generate-Icon.ps1` crops and downsamples the master artwork
  (`src/Blink/Assets/blink-logo-source.png`) into the multi-resolution icon and the
  512 px logo. The vector version lives beside it as `blink-logo.svg`, so every asset
  comes from one source.
- `.github/workflows/build.yml` restores, builds and tests every push and pull request.

## Error handling philosophy

Blink is a convenience utility, so it fails soft:

- A missing desktop list view shows a tray balloon instead of crashing.
- A failed settings write is ignored; defaults apply next launch.
- A failed update check only changes the status label.
- An unreadable wallpaper is skipped during import and listed in the status line; a
  failed apply or a blocked mini overlay sets `LastError`, which the tray shows once as
  a balloon and the windows keep visible until the next action.
- Registry and shortcut failures surface as a message, never an exception dialog.

The only hard requirements are a Windows desktop and a writable user profile.
