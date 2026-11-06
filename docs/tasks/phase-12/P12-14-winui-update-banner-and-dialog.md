# P12-14 - WinUI head: "update available" notice, release notes and restart to update

Status: todo
Depends on: P12-13

## Goal
The WinUI user is told once that an update exists, sees what changed, downloads it with progress, and restarts to apply it. Nothing is ever applied without a click.

## Design decisions (final)
- **Banner**: a slim `InfoBar` between the caption bar and the workspace (below the menu; visibility = `UpdateViewModel.IsBannerVisible`, text = `BannerText`), severity Informational (Warning when `Failed`). Buttons bound to commands: *Details* (opens the update dialog), then by state *Download* (`Available`), a progress bar (`Downloading`: the download cannot be cancelled by the user in this phase; the close button only hides the banner), *Restart to update* (`Ready`), *Retry* (`Failed`); close button = `LaterCommand`. It never takes focus and never steals the editor caret.
- **Dialog** (`UpdateWindow`, modal child like `AboutWindow`): title "SQuilL {version} is available", current vs new version, download size (and "delta" hint when `IsDelta`), the release notes (`ReleaseNotesPresenter` from P12-13, scrollable, max height 280 px), *Download* / *Restart to update* / *Skip this version* / *Later*, progress bar bound to `Progress`, failure text + details under a "Show details" expander.
- **One-time question**: when `UpdateViewModel.ShouldAskFrequency` is true after the main window is shown, a `ContentDialog` (`update.ask.title`/`body`, four buttons: Daily, Weekly, At startup, Never; closing it with Esc = Never is **not** assumed: closing without choosing leaves `NotAsked` and asks again next start). Text mentions GitHub and that nothing else is sent.
- **Restart**: `RestartToUpdateCommand` first asks the workspace to close cleanly: if any query tab has unsaved edits (`WorkspaceViewModel` already knows, P9-11 closing rules), show the existing "unsaved changes" dialog; on cancel, abort the restart. Only then call the service's `RestartToUpdate()`.
- **Startup trigger**: `MainWindow` calls `Updates.StartAsync()` once after the window is activated (fire and forget through the view-model's own error handling; never blocks first paint; never in test/capture runs started with `SQUILL_NO_UPDATE_CHECK=1`, a debug aid documented in `tools/visual/README.md`).
- Accessibility: banner text is announced (`InfoBar` default), buttons have names from the catalog; all strings through `UiText` (keys from P12-11; add `update.dialog.title`, `update.dialog.current`, `update.dialog.new`, `update.dialog.size`, `update.dialog.delta`, `update.dialog.details`, `update.dialog.notes` and `menu.checkForUpdates` in both catalogs; menu `Help > Check for updates...` runs `CheckNowCommand` and opens the dialog when an update is found, or shows the status in a small `TeachingTip`/status bar message when up to date).

## Out of scope
- Avalonia, the release workflow, installing anything real (P12-19).

## Tasks
- [ ] Banner, dialog, question dialog, startup trigger, menu entry, catalog keys fr/en.
- [ ] Tests in `tests/Squill.App.WinUI.Tests`: banner state mapping function (status -> visible buttons) pure and tested for every `UpdateStatus`; restart is refused when unsaved edits exist (view-model level with fakes); scanner/parity green.
- [ ] No fake-engine switch (env var or flag) may be added to the product code. The banner, dialog and question screens are covered by the unit tests above; their real run happens in P12-19 against a local feed. List them under "to check by hand" in the report.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- WinUI AOT publish zero IL2xxx/IL3xxx warnings; the exe starts with a throwaway `SQUILL_DATA_DIR`, shows the one-time question, and with `Never` makes no network call (check with `rtk bypass netstat` or the `read` of the process connections: none to github.com within 30 s); no process left running.

## Suggested commit message
Add the update notice, release notes dialog and restart flow to the WinUI head
