# P21-88 - macOS: first build, Native AOT publish and start of the Avalonia head

Status: todo
Depends on: P21-86

## Goal
Same as P21-86 on macOS (osx-arm64 first, osx-x64 if available): build, test, publish, start, fix the blockers.

Exists today: macOS Keychain store (P2-03), packaging script for macOS (P12-21, never run), nothing ever run on macOS.

## Design decisions (final)
- Environment: a Mac provided by the owner, or a GitHub Actions `macos-15` runner for build/test/publish only (no interactive start). Without a Mac, the card does the CI publish and records the start as `not driven (no Mac)`.
- Checks on a Mac: the app starts unsigned (Gatekeeper: right-click `Open`), menu bar integration, `Cmd` shortcuts (P21-80 displays them; before it, record which keys use `Ctrl`), Keychain prompt on saving a password, SQLite query, LoomTerminal with `zsh`.
- `docs/testing.md` "macOS" section.

## Out of scope
- Notarization and signing (P21-89 open point), driving every screen.

## Tasks
- [ ] CI job (manual dispatch) that publishes osx-arm64; run on a Mac if available; record results.

## Acceptance criteria
- Published osx-arm64 exe from CI; start verified on a Mac or recorded `not driven (no Mac)`; blockers fixed or carded.

## Risks
- `e_sqlite3` and LoomTerminal native assets for osx-arm64 must be in the publish folder: check the file list.

## Open points (defaults)
- Owner: access to a Mac (default: CI only).

## Suggested commit message
Build and publish the Avalonia head for macOS for the first time
