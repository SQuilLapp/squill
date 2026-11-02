# P9-22 - WinUI head: ER diagram view

Status: todo
Depends on: P9-10, P9-02

## Goal
The WinUI ER diagram tab matches M08.

## Design decisions (final)
- Same content rules as P9-21 for `Views/DiagramTabView.xaml` and `Controls/ErDiagramCanvas.cs` (Win2D, bundled JetBrains Mono through P9-01).

## Out of scope
- Layout modes not in the engine, PNG/PDF/Mermaid export (no feature).

## Tasks
- [ ] Rework the view and the canvas drawing.
- [ ] Tests for the testable geometry; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M08-ER-clair-Windows.png`, `M08-ER-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: ER diagram view
