# P21-89 - Linux and macOS: run the packaging and the update flow once

Status: todo
Depends on: P21-86, P21-88

## Goal
P12-21 wrote the Velopack packaging for Linux and macOS but it was never run. Run it, install, update N -> N+1 from a local feed, uninstall, and check the data folder is kept, as P12-19 did on Windows.

Exists today: `eng/` pack scripts for Linux/macOS (P12-21), the Windows end-to-end procedure of P12-19 ([docs/verification/release.md](../../verification/release.md)).

## Design decisions (final)
- Linux: AppImage from `vpk pack` inside WSL; install = run the AppImage; data folder `~/.config/SQuilL` (or what `AppPaths` says) kept across update and removal.
- macOS: `.app` bundle and the Velopack installer pkg from CI; installed only if a Mac is available.
- Never push a `v*` tag (phase 12 rule); local feed only.
- Release workflow: add the Linux job (and macOS job behind a manual flag) only if the runs pass; otherwise leave the workflow unchanged and record why.

## Out of scope
- Code signing and notarization for macOS (needs an Apple developer account: open point).

## Tasks
- [ ] Pack, install, update, uninstall on Linux; macOS when possible; record in `docs/verification/release.md` (new Linux/macOS rows).

## Acceptance criteria
- Linux rows `agent: ok` or bugs carded; macOS rows run or `not driven (no Mac)`.

## Risks
- AppImage needs FUSE in WSL: use `--appimage-extract-and-run` and note it.

## Open points (defaults)
- Owner: Apple developer account for signing and notarization (default: none, unsigned builds documented).

## Suggested commit message
Run the Linux and macOS packages through install, update and uninstall
