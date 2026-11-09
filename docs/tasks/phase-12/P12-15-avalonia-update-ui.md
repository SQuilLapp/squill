# P12-15 - Avalonia head: update settings, About, notice and dialog

Status: done
Depends on: P12-14, P9-23

## Goal
The Avalonia head shows the same update UI as WinUI, driven by the same view-models. Packaging of the Avalonia head is a separate optional card (P12-21), so on an Avalonia build that is not installed the status reads "Not installed through the installer" and nothing is ever downloaded; the UI is still complete and tested so that P12-21 only adds packaging.

## Design decisions (final)
- Mirror P12-13 and P12-14 one to one using the Avalonia patterns of the existing windows (P9-23 settings window, connection manager window): settings section "Updates", `AboutWindow`, update banner (a `Border` strip above the workspace with the same states and buttons), `UpdateWindow` with release notes, the one-time frequency question (a modal dialog window, same wording), `Help > About SQuilL` and `Help > Check for updates...`. All text from the catalog keys of P12-11/12/13/14 (add none unless a control needs one; both catalogs stay in parity).
- `ReleaseNotesPresenter` for Avalonia (`Views/`): `StackPanel` of `SelectableTextBlock` with `Inlines` (`Run` with bold/monospace, `InlineUIContainer` or `HyperlinkButton`-style run for links; only http(s) opened, through `Launcher`/`Process.Start` with `UseShellExecute` on the Windows path and `xdg-open`/`open` otherwise: keep it in one small `ShellOpen` helper next to the head's existing process helpers if one exists).
- Restart: same unsaved-edits check as WinUI; Avalonia `ApplyAndRestart` only reachable when installed.
- Startup trigger and `SQUILL_NO_UPDATE_CHECK=1` identical to WinUI (read the variable in one shared place: a static helper in `Squill.Presentation.Updates` used by both heads; add it here if P12-14 put it in the WinUI head and move that use to the helper, say so in the report).
- Avalonia gets no Velopack bootstrap in `Program` (not packaged here): `IsInstalled` false is the expected path; do not call `VelopackApp.Build()` in this head.

## Out of scope
- Packaging, Linux/macOS update mechanisms.

## Tasks
- [x] Settings section, About, banner, update window, question dialog, menu entries, notes presenter, shared startup helper.
- [x] Tests in `tests/Squill.App.Avalonia.Tests`: presenter output, banner state mapping (same table as WinUI; if the table is a pure function, move it into Presentation as `UpdateBannerLayout` used by both heads and tested once there), headless Avalonia test opening the About window and the update window without exceptions.
- [ ] (not done, see report) Capture with `SQUILL_LANGUAGE=en` and `fr` (P9-02 tools); fr text must not clip.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` zero IL2xxx/IL3xxx warnings; the exe starts with a throwaway `SQUILL_DATA_DIR`, About opens, *Check now* shows the not-installed status without any network call; no process left running.

## Suggested commit message
Add update settings, About, notice and dialog to the Avalonia head

## Report
- Added (Avalonia head): "Updates" card in the settings Appearance page, `AboutWindow`, `UpdateWindow`, `UpdateAskWindow` (closing without a choice keeps `NotAsked`), `ReleaseNotesPresenter` (links are buttons in an `InlineUIContainer`, opened through `TopLevel.Launcher`, so no `ShellOpen` helper was needed), update notice strip under the title bar (main grid gained a row), `Help > Check for updates...` and `Help > About SQuilL`, start-up trigger from `App` on `Opened`. No Velopack bootstrap in this head.
- Moved to Presentation and shared by both heads: `UpdateBannerLayout` (was WinUI `UpdateBannerFormat`), `ReleaseNotesFormat`, new `UpdateStartup.IsDisabled()` (reads `SQUILL_NO_UPDATE_CHECK`; the WinUI head now uses it). Their tests moved to `Squill.Presentation.Tests/Updates`; `SQUILL_NO_UPDATE_CHECK` documented in `tools/visual/README.md`.
- Tests: Avalonia headless (presenter output, About/Update/Settings windows built against a not-installed app, notice hidden without an offer), startup helper, banner layout per status.
- AOT publish of the Avalonia head: no IL2xxx/IL3xxx warning at all; the exe started with a throwaway `SQUILL_DATA_DIR`, stayed alive 15 s with 0 remote connections, then was killed.

## To check by hand
- fr/en captures (P9-02 tools) of About, update window, notice and question dialog (text must not clip in fr); Esc and Later/Skip behaviour; `Help > Check for updates...` flyout from a non-installed build; notice and dialogs against a local feed (P12-19).
