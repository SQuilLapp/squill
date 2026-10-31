# P6-03 - Windows packaging with Velopack and release workflow

Status: done
Depends on: P6-02, P1-02

## Goal
Produce an installer and update feed for the WinUI head.

## Design decisions (final)
- Package `Velopack` 1.2.161 in the WinUI head: `VelopackApp.Build().Run()` as the first statement of `Program` (custom `Main`, `DISABLE_XAML_GENERATED_MAIN`), no auto-update UI yet; `UpdateManager` is not wired (out of scope).
- `eng/pack-windows.ps1` (parameter `-Version`): publishes the AOT win-x64 head to `artifacts/publish` and runs `vpk pack --packId SQuilL --packVersion $Version --packDir artifacts/publish --mainExe Squill.App.WinUI.exe --icon assets/brand/app-icons/windows/squill.ico --outputDir artifacts/releases` (installs `vpk` as a local dotnet tool pinned in `.config/dotnet-tools.json`). `artifacts/` goes in `.gitignore`.
- `.github/workflows/release.yml`: on tag `v*`, windows job runs the script with the tag version and uploads `artifacts/releases` as workflow artifacts (no GitHub Release creation, no signing).

## Out of scope
- Code signing, delta updates, macOS/Linux packages, update UI.

## Tasks
- [x] Add the Velopack bootstrap, script and workflow.
- [x] Run the script with `-Version 0.0.1-test`, check the Setup exe exists, DO NOT run the installer; delete `artifacts/` afterwards.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `artifacts/releases` contains `SQuilL-win-Setup.exe` after the script (then cleaned).

## Suggested commit message
Add Velopack packaging script and release workflow
