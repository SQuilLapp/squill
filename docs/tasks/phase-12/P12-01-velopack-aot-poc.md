# P12-01 - Velopack under Native AOT: proof of concept and report

Status: todo
Depends on: none

## Goal
Prove, before any design depends on it, that the `Velopack` package (1.2.161, already referenced by the WinUI head since P6-03, used only for `VelopackApp.Build().Run()`) works under Native AOT with the update API (`UpdateManager`), and settle every open question of the phase in one report. Findings only: no product code stays in the repository.

## Design decisions (final)
- Work in a throwaway console project under the scratchpad / `artifacts/poc-velopack/` (git-ignored), `PublishAot`, `net11.0`, `win-x64`, `PackageReference Velopack` 1.2.161, **not** added to any solution. Delete it at the end.
- Questions to answer, each with evidence (command + result) in `docs/poc/velopack-aot.md`:
  1. Does `dotnet publish -r win-x64` with `UpdateManager`, `GithubSource`, `SimpleFileSource` (or the file/web source the package offers), `CheckForUpdatesAsync`, `DownloadUpdatesAsync`, `ApplyUpdatesAndRestart` give zero IL2xxx/IL3xxx warnings? List every warning, with the package assembly responsible.
  2. Does the same code, packed with `dotnet vpk pack`, installed (`Setup.exe`, throwaway pack id `SQuilLPoc`, uninstalled at the end) and run, actually check, download and apply an update from a local directory feed (two packed versions `0.0.1` and `0.0.2`, same channel)? (The POC console app prints `UpdateManager.IsInstalled`, `CurrentVersion`, the check result, progress and applies.)
  3. The exact public API names and signatures of the pinned version that later cards use (`UpdateManager`, `UpdateOptions.ExplicitChannel`, `UpdateInfo`, `VelopackAsset` notes properties for markdown, `IsInstalled`, `UpdatePendingRestart`, progress callback type, source classes). Record them in a table: later cards copy them from the report instead of guessing.
  4. Feed format and channel behaviour: file names produced by `vpk pack --channel stable-x64` (Setup, `.nupkg` full and delta, `releases.<channel>.json`); whether `vpk download github` followed by `vpk pack` produces a delta; how `GithubSource` selects the release (latest, pre-release flag) and which anonymous GitHub API call it makes (rate-limit risk at one call per check).
  5. Which `vpk pack` options exist for: `--channel`, `--packTitle`, `--packAuthors`, `--releaseNotes`, `--icon`, `--shortcuts`, `--runtime`/`--framework` (self-contained AOT means no runtime prerequisite), signing (`--signParams`, `--azureTrustedSignFile`), `--delta`. Record the real names.
  6. Hooks: `VelopackApp.Build()` `OnFirstRun`, `OnBeforeUpdateFastCallback`, `OnAfterUpdateFastCallback`, `OnBeforeUninstallFastCallback` (or the names of the pinned version): which exist, what arguments, time limit (Velopack kills hooks after a few seconds), whether they run before `Main` continues.
  7. Does Velopack touch `%APPDATA%\SQuilL`? (It must not: install root is `%LOCALAPPDATA%\<packId>`.) Show the folder listing after install, update and uninstall.
  8. arm64: does `dotnet publish src/Squill.App.WinUI -c Release -r win-arm64` succeed on this machine (Native AOT cross-compilation needs the VS ARM64 build tools)? If yes, does the result start? If no, state the missing prerequisite and whether GitHub's `windows-11-arm` runner could do it. Verdict for P12-18: go / no-go / go-on-CI-only.
  9. Where the update adapter can live: the answer to "can `Squill.Platform` (`IsAotCompatible`, warnings as errors) reference `Velopack` and keep zero warnings?" (build a tiny library project with the same props to check). Verdict: Platform, or the heads.
- Stop rule: if question 1, 2 or 9 fails, stop with the report and a proposed rewrite of P12-07 ... P12-15 (for example a hand-written feed client over `HttpClient` + source-generated JSON, no `UpdateManager`); do not improvise.

## Out of scope
- Any change to `src/`, `eng/` or the workflows; real GitHub Releases; signing.

## Tasks
- [ ] Build the POC, answer the nine questions, uninstall everything, delete the project and `artifacts/poc-velopack/`.
- [ ] Write `docs/poc/velopack-aot.md` (verdicts first, then evidence, then the API table of question 3).
- [ ] Add a "Velopack" row to the risk table of `docs/architecture.md` (R10 or next free) pointing to the report.
- [ ] If an answer contradicts `release-recipe.md` (channel names, delta flow, hooks), edit the recipe and say which line in the report.

## Acceptance criteria
- Report complete: nine answers, each with a verdict and evidence; no empty section.
- `rtk git status` shows only `docs/` changes; `%LOCALAPPDATA%\SQuilLPoc` is gone, no process left running (`dotnet build-server shutdown`).

## Suggested commit message
Prove Velopack updates under Native AOT and record the findings
