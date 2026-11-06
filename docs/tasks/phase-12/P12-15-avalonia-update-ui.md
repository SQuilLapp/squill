# P12-15 - Avalonia head: update settings, About, notice and dialog

Status: todo
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
- [ ] Settings section, About, banner, update window, question dialog, menu entries, notes presenter, shared startup helper.
- [ ] Tests in `tests/Squill.App.Avalonia.Tests`: presenter output, banner state mapping (same table as WinUI; if the table is a pure function, move it into Presentation as `UpdateBannerLayout` used by both heads and tested once there), headless Avalonia test opening the About window and the update window without exceptions.
- [ ] Capture with `SQUILL_LANGUAGE=en` and `fr` (P9-02 tools); fr text must not clip.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` zero IL2xxx/IL3xxx warnings; the exe starts with a throwaway `SQUILL_DATA_DIR`, About opens, *Check now* shows the not-installed status without any network call; no process left running.

## Suggested commit message
Add update settings, About, notice and dialog to the Avalonia head
