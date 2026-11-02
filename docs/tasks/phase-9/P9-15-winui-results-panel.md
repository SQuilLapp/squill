# P9-15 - WinUI head: results panel chrome, pending-edit banner and export menu

Status: todo
Depends on: P9-12, P8-17, P9-02

## Goal
Everything around the WinUI grid matches M05: result tabs, read/write marker, pending-edits banner, export menu, footer.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 6 (final). Depends on P8-17 being done (its edit bar is what this card restyles); if it is not done, stop with a report.
- Same content and rules as P9-13 for WinUI: `Exporter` dropdown (`MenuFlyout`), pending-edits banner replacing the edit bar with the same commands, icon buttons for add/delete/revert, read/write marker, pill result tabs, footer.

## Out of scope
- Grid canvas drawing (P9-16), filter bar, column menu, row panel (no feature).

## Tasks
- [ ] Rework `Views/QueryTabView.cs` results part.
- [ ] Tests for the testable helpers (banner text counts, marker text); state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M05-Grille-clair-Windows.png`, `M05-Grille-sombre-Windows.png`, `M03-Workspace-*-Windows.png` (results header) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: results panel chrome, pending-edit banner and export menu
