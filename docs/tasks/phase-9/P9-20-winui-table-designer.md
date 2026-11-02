# P9-20 - WinUI head: table designer view

Status: todo
Depends on: P9-10, P9-02

## Goal
The WinUI table designer matches M07.

## Design decisions (final)
- Same content rules as P9-19 for `Views/TableDesignerView.xaml`; DDL panel highlighting with the highlighter the head already uses for scripts (read `Editors/`; plain mono text if none exists, and say so).

## Out of scope
- Impact card, extra tabs, rollback option (no feature).

## Tasks
- [ ] Rework `TableDesignerView.xaml(.cs)`.
- [ ] Tests for the testable helpers; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M07-Table-clair-Windows.png`, `M07-Table-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: table designer view
