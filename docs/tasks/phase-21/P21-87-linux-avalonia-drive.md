# P21-87 - Linux: verification pass of the Avalonia head

Status: todo
Depends on: P21-86

## Goal
Go through the main screens on Linux once, with the owner's checklists as the list, and record what works.

Exists today: the verification checklists in `docs/verification/` (agent column rule of phase 20), `tools/visual/Drive-Window.ps1` (Windows UI Automation: **does not work on Linux**).

## Design decisions (final)
- Driving on Linux: `xdotool` (X11/XWayland under WSLg) for keys and clicks, `import` (ImageMagick) for captures; a small `tools/visual/linux/drive.sh` wrapper only if more than a handful of commands are repeated.
- Scope: connection manager, explorer and filter popup, query tab (run, cancel, parameters, completion), grid (edit, commit, filter, export), designer, ER, plan (SQLite), settings (language, theme), terminal tab (LoomTerminal on Linux: `sqlite3`), SSH tunnel to a container (Podman in WSL or Docker), update banner off.
- Results in a new `docs/verification/linux.md` (agent column only, owner boxes empty).

## Out of scope
- macOS.

## Tasks
- [ ] Drive, capture, record; card bugs `P21-97+`.

## Acceptance criteria
- `docs/verification/linux.md` with every step `agent: ok / bug / not driven (<why>)`.

## Risks
- Fonts: bundled fonts must be used (no fallback to a system font); check the captures.

## Open points (defaults)
- None.

## Suggested commit message
Record a first verification pass of the Avalonia head on Linux
