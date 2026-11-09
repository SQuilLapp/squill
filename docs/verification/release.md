# Release verification: install, update, uninstall (P12-19)

Run on Windows 11 x64 with `rtk pwsh eng/verify-update.ps1` (reusable: it packs `0.0.1` and `0.0.2` as pack id `SQuilLVerify` into a local feed, installs `0.0.1` silently, drives the window through UI Automation, uninstalls, cleans up). No GitHub Release, no tag, no network feed. Velopack 1.2.161, WinUI head, `SQUILL_LANGUAGE=en`, `SQUILL_DATA_DIR` and `SQUILL_UPDATE_FEED` set by the script, settings seeded with the check frequency `Startup`.

## Result

| Step | Result | Note |
|---|---|---|
| 1 Pack `0.0.1`, then `0.0.2` into one feed | ok | `pack-windows.ps1` empties its output directory when it publishes, so the script publishes `0.0.2` itself and packs with `-SkipPublish` (the feed keeps `0.0.1` and vpk builds the delta) |
| 2 Feed content: Setup, two full packages, one delta, `releases.stable-x64.json` lists both | ok | |
| 3 Seed the data directory (settings, `history.json`, a 200-byte marker file) | ok | |
| 4 `Setup-0.0.1.exe --silent`: `%LOCALAPPDATA%\SQuilLVerify\current\Squill.App.WinUI.exe` has product version `0.0.1`, `Update.exe` present | ok | |
| 5 Start `0.0.1`: banner "SQuilL 0.0.2 is available" within 25 s; *Download* then *Restart to update* appears; click it | ok | clicked through UI Automation (buttons carry their text as automation name); the app exits while the click is delivered, which UI Automation reports as an error that the script expects |
| 6 After the relaunch: exe is `0.0.2`, seeded files byte-identical, `install-state.json` says `0.0.2`, About shows `Version 0.0.2` and `Updated from 0.0.1` | ok | the one-time "what's new" line is the run kind `Updated` |
| 7 No connection outside this machine during 30 s of a start with `Startup` checks | ok | sampled every second with `Get-NetTCPConnection` on the second start only (not the whole run); the feed was a directory, so this proves the local override works, not that GitHub is never called |
| 8 Second start finds nothing newer: no banner | ok | the "About says up to date" wording was not driven |
| 9 `Update.exe uninstall --silent`: install root gone, no Start menu or desktop shortcut left, data directory intact (same hashes) | ok | the Velopack log `%LOCALAPPDATA%\velopack\velopack_SQuilLVerify.log` stays by design |
| 10 Cleanup: no process left, install root and shortcuts gone, scratch and `artifacts/` removed | ok | |

Not covered by the script: that the delta package (not the full one) was the file downloaded (the feed contains it; the choice is Velopack's); the version check against real GitHub Releases (which release wins when several carry the channel: verify at the first real release); signing (P12-17), arm64 (P12-18) and the Avalonia head.

No bug found; nothing was changed in `src/`.

## To check by hand (owner, about 10 minutes)

The shortcut the installer creates does not carry the environment variables of the script. For steps that need an installed app, run `rtk pwsh eng/verify-update.ps1 -KeepInstalled` (it leaves `SQuilLVerify` installed on `0.0.2`, the scratch folder and the data), or install `artifacts/verify/Setup-0.0.1.exe` from a previous `-KeepInstalled` run. Uninstall at the end from Windows *Apps & features*, then delete `artifacts/verify` and `%LOCALAPPDATA%\velopack\velopack_SQuilLVerify.log`.

- [ ] Double-click the installed shortcut (Start menu): the app opens on the real data directory (`%APPDATA%\SQuilL`), not the throwaway one.
- [ ] Run the unsigned `Setup.exe` by double-click: Windows SmartScreen shows "Windows protected your PC" (expected while unsigned); *More info* then *Run anyway* installs.
- [ ] Open a query tab, type some text without saving, then use *Help > About > Check now* with a newer version in the feed and click *Restart to update*: expected result is a prompt about the unsaved edits before the restart (if the app restarts without asking, write a bug card).
- [ ] *Settings > Updates*: set the channel to *Beta*, press *Check now*: the status line answers (up to date or a beta version) with no error.
- [ ] Windows *Settings > Apps > Installed apps* lists `SQuilL` (the pack title; the id `SQuilLVerify` is the folder name) with the right version, and *Uninstall* removes it; `%APPDATA%\SQuilL` (or the throwaway data directory) is still there.
- [ ] *Help > About*: the "Open data folder" link opens the data directory; "Repository on GitHub" opens the repository.
- [ ] With the frequency set to *Never*, start the app with the network cable unplugged: no banner, no error dialog.
