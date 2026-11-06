# P12-18 - Windows arm64 build and package (conditional)

Status: todo
Depends on: P12-16, P12-01

## Goal
Ship a `win-arm64` installer and feed next to `win-x64`, if Native AOT allows it. Gate: the verdict of P12-01 question 8.

## Design decisions (final)
- **Stop rule**: if P12-01 answered "no-go", set this card's status to `skipped` with the reason, mark the index row `skipped`, commit only those two edits (docs) and finish. If "go-on-CI-only", skip the local publish steps and verify through the workflow (below).
- `src/Squill.App.WinUI.csproj`: keep `RuntimeIdentifier` win-x64 as the default; the pack script passes `-r win-arm64` explicitly. Check that every native dependency has an arm64 build: Windows App SDK (self-contained), Win2D, LoomEditor.WinUI, LoomTerminal.WinUI (conpty natives), the Oracle bridge (`eng/OracleBridge.targets`), SQLite native (`SQLitePCLRaw`). List each with its arm64 status in the report; any x64-only native library makes the verdict **no-go** (skipped as above, with the blocker named).
- `eng/pack-windows.ps1 -Runtime win-arm64`: publish folder `artifacts/publish-arm64`, Velopack channel `<channel>-arm64`, same pack id; the runtime validation set gains `win-arm64`; asset names carry the channel (recipe section 3) so both architectures coexist in one Release. The script's output check applies per runtime.
- The app side already derives the channel from `RuntimeInformation.ProcessArchitecture` (P12-09); add a unit test row if one is missing for `Arm64`.
- Workflow: a second job `windows-arm64` on `windows-11-arm` (if the runner label exists for this repository: check with `gh api` or the docs; otherwise cross-publish from `windows-latest` when P12-01 says it works), same steps, `-Runtime win-arm64`; the Release creation (P12-16) must wait for both jobs (`needs`) and upload both sets; json files of both architectures last.
- Running the arm64 exe is not possible on this machine unless it is arm64: say so; verification is "publish succeeds, zero IL warnings, `dumpbin /headers` (or `Get-Content -Encoding Byte` PE machine field) says ARM64".

## Out of scope
- Avalonia arm64, signing differences, any feature work.

## Tasks
- [ ] Gate check, audit table, script and workflow changes.
- [ ] Local run (background) when possible: `rtk pwsh eng/pack-windows.ps1 -Version 0.0.1-test -AllowVersionOverride -Runtime win-arm64`; check the PE machine type; delete `artifacts/`.

## Acceptance criteria
- Either: arm64 pack produced with zero IL2xxx/IL3xxx warnings, machine type ARM64, workflow dry run green (or "not verified" with URL); or: card marked `skipped` with the named blocker.
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; x64 packing unchanged (rerun the x64 pack of P12-05 quickly with `-SkipPublish` off only if the script's x64 path changed).

## Suggested commit message
Package the WinUI head for Windows arm64
