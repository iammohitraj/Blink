# Changelog

All notable changes to Blink are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-10-31

### Changed

- New brand artwork: the app, tray and installer icons and the 512 px logo are
  generated from a new master image, with a tighter crop at small sizes.
- Added a vector version of the logo at `src/Blink/Assets/blink-logo.svg`.
- Raised the UI to a higher-contrast dark palette with accessible text contrast,
  visible focus rings and keyboard activation for buttons and switches.
- Stronger menu selection, tooltips, switch tracks, status dot and progress bar.

## [1.1.0] - 2026-10-30

### Added

- `Blink.Core`, a dependency-free data structures and algorithms library covering
  dynamic arrays, stacks, queues, dequeues, ring buffers, heaps, an open-addressed
  hash map and set, an LRU cache, union-find, BST, AVL tree, trie, graphs (BFS, DFS,
  Dijkstra, topological sort), sorting, searching, KMP, Rabin-Karp and number theory.
- A 27-test xUnit suite for `Blink.Core`.
- A benchmark harness that reports elapsed time and bytes allocated per operation.
- SemVer 2.0 parsing and ordering, including prerelease precedence.
- `docs/ARCHITECTURE.md` and `docs/ALGORITHMS.md`.
- CI workflow, issue forms, pull request template and Dependabot configuration.

### Changed

- Repository reorganised into `src/`, `tests/`, `benchmarks/`, `docs/` and `.github/`,
  with a solution file at the root.
- Update checks now compare semantic versions instead of `System.Version`.
- The icon cache is a bounded LRU cache instead of an unbounded dictionary.
- Update downloads rent their buffer from `ArrayPool<byte>`.
- Window class-name lookups reuse a thread-static buffer.
- The double-click watcher uses `PeriodicTimer`; the settings listener uses
  `WaitHandle.WaitAny`, removing two polling loops.
- Settings saves reuse a static `JsonSerializerOptions` and swap files atomically.
- Installer and settings windows show the author credit.
- The release script runs the test suite before packaging.

## [1.0.0] - 2026-10-24

### Added

- Instant desktop icon toggle via `Ctrl + Alt + D`, wallpaper double-click, or the
  tray icon.
- Dark themed settings window and installer.
- Per-user install with optional start-with-Windows and desktop shortcut.
- Silent install and uninstall command-line modes.
- Automatic updates from GitHub Releases.
- Single-instance handling: launching Blink again opens the running copy's settings.
