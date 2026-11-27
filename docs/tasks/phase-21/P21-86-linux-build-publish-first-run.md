# P21-86 - Linux: first build, Native AOT publish and start of the Avalonia head

Status: todo
Depends on: none

## Goal
The Avalonia head was **never run on Linux** (P12-21 done but not run; "Left open by phase 20" item 12). Build, test, publish and start it on a real Linux desktop for the first time and fix what blocks the start.

Exists today: `Squill.slnx` is meant to build everywhere; CI builds and tests on Linux (P1-02, P11-05) but never publishes or starts the app; libsecret store (P2-02, source-generated P/Invoke); `e_sqlite3` native asset; LoomTerminal Avalonia package; packaging script for Linux (P12-21).

## Design decisions (final)
- Environment: WSL2 with WSLg on the owner's Windows machine (Ubuntu 24.04), or a GitHub Actions `ubuntu-24.04` runner for build/publish only. Native AOT cannot cross-compile from Windows to Linux: publish **inside** Linux (`clang`, `zlib1g-dev` installed).
- Steps: `dotnet build Squill.slnx -c Release -warnaserror`, `dotnet test` (Docker-gated off), `dotnet publish src/Squill.App.Avalonia -c Release -r linux-x64` (zero new IL2xxx/IL3xxx), start the exe under WSLg, open the SQLite scenario, run a query, open the designer and the ER diagram, save a connection with a password (libsecret through `gnome-keyring`; if no keyring daemon, the error must be readable).
- Every blocker found is fixed in this card when small (with a test where possible) or carded `P21-97+`.
- `docs/testing.md` gets a "Linux" section with the exact packages and commands.

## Out of scope
- Driving every screen (P21-87), macOS (P21-88), packaging (P21-89).

## Tasks
- [ ] Build, test, publish and start; record each command and its result in the Done notes; captures of the main window (WSLg window capture with `import` or the Windows capture tool on the WSLg window).

## Acceptance criteria
- The published linux-x64 Avalonia exe starts and runs a SQLite query; warnings baseline recorded; blockers fixed or carded.

## Risks
- WSLg is not a real desktop (no full theme/portal support): note any difference that only WSLg could explain.

## Open points (defaults)
- Owner: is WSLg enough for the first run (default yes), or a real Linux machine/VM.

## Suggested commit message
Build, publish and start the Avalonia head on Linux for the first time
