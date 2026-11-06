# P12-05 - Pack script: AOT publish and `vpk pack` for a channel

Status: todo
Depends on: P12-01, P12-02, P12-04

## Goal
`eng/pack-windows.ps1` (P6-03) becomes the one release build entry point, used by the developer and by CI: version from the single source, channel, release notes, previous-release deltas, output checks. Signing hooks are added by P12-17, arm64 by P12-18.

## Design decisions (final)
- Parameters: `-Version` (default: `eng/get-version.ps1`), `-Channel stable|beta` (default: `beta` when the version has a pre-release suffix, else `stable`), `-Runtime win-x64` (validated set, only `win-x64` until P12-18), `-PackId SQuilL`, `-OutputDir artifacts/releases`, `-FromGithub` (switch: download the previous release of the Velopack channel before packing, so deltas are produced; needs `GITHUB_TOKEN` or `gh auth token` only to avoid rate limits; absent token = anonymous), `-SkipPublish` (reuse `artifacts/publish`; used by the second channel pass), `-AlsoBeta` (stable only: after the stable pack, pack the same publish output again for the `beta` channel, recipe section 3).
- Steps: (1) validate the version with `eng/get-version.ps1`-style regex (the script may only be run with `-Version` different from `eng/Version.props` when `-AllowVersionOverride` is given: the local verification of P12-19); (2) `dotnet publish src/Squill.App.WinUI -c Release -r <rid> -o artifacts/publish -p:Version=<v>` (warnings stay errors; the script fails on any IL2xxx/IL3xxx line); (3) release notes file from `eng/release-notes.ps1 -Version <v> -OutFile artifacts/release-notes.md` (`Unreleased` allowed only with `-AllowVersionOverride`); (4) `dotnet tool restore`; (5) optional `dotnet vpk download github --repoUrl https://github.com/squillapp/squill --channel <vpkChannel> --outputDir <OutputDir>` (tolerate "no release yet" without failing, fail on any other error); (6) `dotnet vpk pack --packId SQuilL --packVersion <v> --packTitle SQuilL --packAuthors SQuilL --packDir artifacts/publish --mainExe Squill.App.WinUI.exe --icon assets/brand/app-icons/windows/squill.ico --channel <channel>-x64 --releaseNotes artifacts/release-notes.md --outputDir <OutputDir>` (flag names as recorded by P12-01; `--shortcuts` only if it differs from the default).
- Output check at the end (throws with the missing name): `Setup.exe` for the channel, the full `.nupkg` for `<v>`, `releases.<channel>-x64.json` listing `<v>`; with `-FromGithub` and a previous release, also a delta `.nupkg`. Prints a table (file, size) at the end.
- A Windows runner restriction: the script uses only `pwsh` 7, `dotnet`, `vpk`; no other tool. All paths relative to the repository root found from `$PSScriptRoot`.
- `README.md` build section updated (the P6-03 paragraph). The release notes of the pack come from `CHANGELOG.md`, so a local pack of the current `eng/Version.props` version needs its section (documented).

## Out of scope
- Uploading anything (P12-16), signing (P12-17), arm64 (P12-18), running the installer (P12-19).

## Tasks
- [ ] Rewrite the script per above; keep `artifacts/` git-ignored.
- [ ] Run in the background: `rtk pwsh eng/pack-windows.ps1 -Version 0.0.1-test -AllowVersionOverride` (notes `Unreleased`), check the output table; run it a second time with `-Version 0.0.2-test -AllowVersionOverride -FromGithub` only to confirm the "no release yet" path does not fail. Delete `artifacts/` afterwards. DO NOT run the installer.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings (script-only change, build unchanged).
- The first run leaves `SQuilL-beta-x64-Setup.exe` (name as vpk produces it, recorded in the report), the full nupkg and the channel json in `artifacts/releases`; the script failed loudly when one of them was missing (prove by deleting one in a dry run of the check function, or by a `-SelfTest` of the check).
- No process left running (`dotnet build-server shutdown`).

## Suggested commit message
Make the pack script the single release build entry point
