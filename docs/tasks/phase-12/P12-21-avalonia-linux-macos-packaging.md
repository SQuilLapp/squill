# P12-21 - Avalonia head: Linux and macOS packaging with Velopack (optional)

Status: todo
Depends on: P12-15, P12-16, P12-20

## Goal
Optional, last card of the phase: package the Avalonia head for Linux (`linux-x64`, AppImage through Velopack) and macOS (`osx-arm64`, `.app` / pkg through Velopack) and publish them in the same GitHub Release, so the Avalonia head can update itself where Velopack supports it. Skip the card (status `skipped`, reason in the index) if the owner does not need those platforms or if one of the stop rules below triggers.

## Design decisions (final)
- Gate, checked first and recorded in the report: (1) Velopack 1.2.161 supports `vpk [linux]` and `vpk [osx]` packing for a Native AOT Avalonia app (P12-01 question 5 listed the flags; re-check `dotnet vpk pack --help` on the target OS); (2) packing for Linux and macOS must run on a Linux / macOS runner (`ubuntu-latest`, `macos-latest`); this machine is Windows, so **nothing here can be run locally**: verification is the workflow run only. If the owner has no way to check a result on a real Linux/macOS desktop, the report must say "published, not run".
- `eng/pack-unix.ps1 -Runtime linux-x64|osx-arm64 -Version -Channel` (PowerShell 7 runs on both OSes, keeps one scripting language): `dotnet publish src/Squill.App.Avalonia -c Release -r <rid> -o artifacts/publish-<rid>`, `vpk pack` with the Linux/macOS icon (`assets/brand/app-icons/hicolor` PNGs for Linux, `squill.icns` for macOS), main exe `Squill.App.Avalonia`, channel `<channel>-<arch>` with arch `x64` for linux-x64 and `arm64` for osx-arm64 (recipe section 3, the Windows channels are unchanged and the runtime part of the channel name is read from `RuntimeInformation` by the engine already).
- `Squill.App.Avalonia` `Program.cs`: add `VelopackApp.Build().Run()` as its first statement now (not before: P12-15 deliberately left it out); the Avalonia head references `Velopack` through `Squill.Platform` only (no direct package reference unless the compiler demands it).
- Workflow: jobs `linux` and `macos` in `release.yml` (`needs` them in the Release creation of P12-16, json files last), each `-DryRun`-able; macOS notarization and signing are **out of scope** (Gatekeeper will warn; state it in `docs/release.md`).
- UI: the existing update UI of P12-15 is reused unchanged; the About text `about.install.*` already covers installed vs not.
- `UpdateEngineFactory` already builds the channel with the process architecture: add the `osx` arm64 / `linux` x64 rows to its unit test.

## Out of scope
- Windows packaging of the Avalonia head, Flatpak/Snap/deb/rpm/Homebrew, notarization, arm64 Linux.

## Tasks
- [ ] Gate check and report; `eng/pack-unix.ps1`; `Program.cs`; workflow jobs; test row; `docs/release.md` section "Linux and macOS (Avalonia)" with the limitations.
- [ ] Push the branch and run `rtk gh workflow run release.yml --ref phase-12-release -f dry_run=true`; check the two new jobs once with `rtk gh run list`; if not finished, report "not verified" with the URL.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; Avalonia publish on Windows still zero IL2xxx/IL3xxx warnings with the bootstrap added.
- The dry-run jobs are green or the report states which step fails and why. Nothing is claimed as run on Linux or macOS unless a human did.

## Suggested commit message
Package the Avalonia head for Linux and macOS with Velopack
