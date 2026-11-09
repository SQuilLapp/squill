# P12-21 - Avalonia head: Linux and macOS packaging with Velopack (optional)

Status: done
Depends on: P12-15, P12-16, P12-20

## Goal
Optional, last card of the phase: package the Avalonia head for Linux (`linux-x64`, AppImage through Velopack) and macOS (`osx-arm64`, `.app` zipped) and publish them in the same GitHub Release, so the Avalonia head can update itself on Linux (macOS has no feed yet). Skip the card (status `skipped`, reason in the index) if the owner does not need those platforms or if one of the stop rules below triggers.

## Design decisions (final)
- Gate, checked first and recorded in the report: (1) Velopack 1.2.161 supports `vpk [linux] pack` (AppImage) and `vpk [osx] bundle` (`.app`) for a Native AOT Avalonia app (checked with `dotnet vpk [linux] pack -h` and `[osx] bundle -h`; `[osx] pack` is not offered when cross-compiling from Windows); (2) packing for Linux and macOS must run on a Linux / macOS runner (`ubuntu-latest`, `macos-latest`); this machine is Windows, so **nothing here can be run locally**: verification is the workflow run only. The report must say "published, not run".
- **Channel names (card fix)**: Velopack file names (`releases.<channel>.json`, `SQuilL-<ver>-<channel>-full.nupkg`, `SQuilL-<channel>-Setup.*`) carry no OS, so Linux `stable-x64` would collide with the Windows files in the one Release. Off Windows the OS is part of the channel: `stable-linux-x64`, `beta-linux-x64`, `stable-osx-arm64`, `beta-osx-arm64`. Windows names stay `stable-x64` / `stable-arm64`.
- `VelopackUpdateEngine.ChannelName(channel, architecture, os)` takes the OS infix (`linux`, `osx`, null/empty on Windows); the engine passes the current OS.
- `eng/pack-unix.ps1 -Runtime linux-x64|osx-arm64` (PowerShell 7 on both OSes): `dotnet publish src/Squill.App.Avalonia -c Release -r <rid> -o artifacts/publish-<rid>`; Linux: `vpk pack` with `assets/brand/app-icons/linux/hicolor/256x256/apps/squill.png`, main exe `Squill.App.Avalonia`; macOS: `vpk bundle` with `squill.icns`, then the `.app` is zipped (`SQuilL-<version>-osx-arm64.zip`, no feed, no automatic update). Output `artifacts/releases-<rid>`. No delta packages.
- `Squill.App.Avalonia` `Program.cs`: `VelopackApp.Build().SetAutoApplyOnStartup(false).Run()` first (the Avalonia head reaches `Velopack` through `Squill.Platform`).
- Workflow: job `pack-unix` (matrix ubuntu / macos) in `release.yml`, a dependency of the Release job whose failure never blocks the Windows release; artifact names `squill-linux-x64`, `squill-osx-arm64`; feed files are still uploaded last by `publish-release.ps1`. macOS signing and notarization are **out of scope** (stated in `docs/release.md`).
- UI: the update UI of P12-15 is reused unchanged.

## Out of scope
- Windows packaging of the Avalonia head, Flatpak/Snap/deb/rpm/Homebrew, notarization, arm64 Linux, macOS update feed and delta packages.

## Tasks
- [x] Gate check and report; `eng/pack-unix.ps1`; `Program.cs`; workflow job; OS-aware `ChannelName` and test rows; `docs/release.md` section "Linux and macOS (Avalonia)" with the limitations; recipe section 3.
- [x] Push the branch and run `rtk gh workflow run release.yml --ref <branch> -f dry_run=true`; the result of the two `pack-unix` jobs is in the commit report ("not verified" when not finished).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; Avalonia publish on Windows still zero IL2xxx/IL3xxx warnings with the bootstrap added.
- The dry-run jobs are green or the report states which step fails and why. Nothing is claimed as run on Linux or macOS unless a human did.

## Suggested commit message
Package the Avalonia head for Linux and macOS with Velopack
