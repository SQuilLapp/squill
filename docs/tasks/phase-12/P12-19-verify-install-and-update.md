# P12-19 - End-to-end verification: install, update from N to N+1, uninstall

Status: todo
Depends on: P12-05, P12-06, P12-09, P12-14, P12-16, P12-15

## Goal
Prove on this machine, with a local feed and no GitHub Release, that a user can install SQuilL with `Setup.exe`, be told about a newer version, download it (delta), restart into it, keep all their data, and uninstall cleanly. Write the report with a manual checklist for the owner.

## Design decisions (final)
- Method: `release-recipe.md` sections 3 and 4, plus `docs/tasks/phase-11/verification-recipe.md` section 2 for the report format (`docs/verification/release.md`: table `Step | Result | Note`, results `ok`, `fixed in <commit>`, `bug -> <new card id>`, `not driven (why)`).
- Isolation so the owner's real SQuilL (if any) is untouched: pack id `SQuilLVerify` (`-PackId`), data directory `SQUILL_DATA_DIR=<scratch>\data`, feed `SQUILL_UPDATE_FEED=<scratch>\feed`, installer silent (flag from P12-01). The installed app's exe is started by the script with these environment variables (the installer-created shortcut does not carry them: say so in the report).
- Script `eng/verify-update.ps1` (committed, reusable by the owner): parameters `-Scratch <dir>` (default `artifacts/verify`), `-KeepInstalled`. Steps: (1) pack `0.0.1` then `0.0.2` (same channel `stable`, `-AllowVersionOverride`, `-PackId SQuilLVerify`, both into `<Scratch>\feed`, so vpk produces the `0.0.2` delta); (2) assert feed content (Setup, two full nupkgs, one delta, channel json listing both); (3) pre-seed `<Scratch>\data\settings.json` with frequency `Startup` and some marker data (a profile file, a history line) to prove preservation; (4) install `0.0.1` silently; assert `%LOCALAPPDATA%\SQuilLVerify\current\Squill.App.WinUI.exe` has product version `0.0.1`; (5) start it with the environment, drive it with the P9-02 tools: the banner "SQuilL 0.0.2 is available" appears within 15 s, *Download* shows progress then *Restart to update*, click it; (6) after the relaunch assert the exe in `current` is `0.0.2`, the About window text says `Version 0.0.2`, the marker data is byte-identical, `install-state.json` says `0.0.2` and the run kind was `Updated` (the "what's new" line shows); (7) check no network call to github.com happened during the whole run (`Get-NetTCPConnection` sampling of the process, 30 s) because the feed was local; (8) a second start finds nothing newer: banner hidden, About says up to date; (9) uninstall silently (`Update.exe --uninstall`, flag from P12-01), assert the install folder is gone, the Start menu / desktop shortcut gone, the data folder and marker data still exist; remove them last; (10) `dotnet build-server shutdown`, no leftover process, scratch folder removed unless `-KeepInstalled`.
- Steps 5 and 6 need UI driving: if the P9-02 tools cannot click the banner buttons reliably, mark those rows `not driven (why)` and list them under "to check by hand"; steps 1-4 and 7-10 are automated and must be green.
- Rows to add by hand-only checks (owner, max 10 steps, in the report): double-click the installed shortcut, SmartScreen behaviour of the unsigned `Setup.exe` (expected warning when unsigned), update with an open query tab that has unsaved edits (restart must ask), choose channel Beta then *Check now*, Windows "Apps & features" entry shows `SQuilLVerify` with the right version and uninstalls.
- Fix policy: a bug under ~30 lines with a regression test is fixed here; otherwise a new card `P12-22` onwards (status todo, added to the index).

## Out of scope
- A real GitHub Release or tag, signing, arm64, Avalonia.

## Tasks
- [ ] `eng/verify-update.ps1`; run it in the background (about 15 minutes: two AOT publishes), then write `docs/verification/release.md`.
- [ ] Owner checklist section with exact clicks.

## Acceptance criteria
- Report complete (every step has a result); steps 1-4 and 7-10 automated and green.
- `%LOCALAPPDATA%\SQuilLVerify` is gone, `artifacts/` removed (except if `-KeepInstalled`), no process left running, no Start menu entry left.
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green (only changed if a bug was fixed).

## Suggested commit message
Verify install, update and uninstall end to end and record the report
