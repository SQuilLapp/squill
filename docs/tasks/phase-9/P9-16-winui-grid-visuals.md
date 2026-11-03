# P9-16 - WinUI head: Win2D grid drawing

Status: done
Depends on: P9-15, P8-16, P9-02

## Goal
The Win2D grid draws like M05: fonts, header with types, row states, number column, selection, NULL, density.

## Design decisions (final)
- Same drawing rules as P9-14 applied to `Controls/DataGridCanvas.cs` (Win2D `CanvasControl`), with `CanvasTextFormat` using the bundled JetBrains Mono URI of P9-01 (verify the font really renders; else stop with a report).
- Depends on P8-16 being done (it edits the same file); if it is not, stop.

## Out of scope
- Column resizing, column menu, filters, value panel (no feature).

## Tasks
- [x] Update the drawing; extend `GridEditGeometryTests`-style tests for the new geometry (number column width, state bar rectangles, header type text placement).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M05-Grille-clair-Windows.png`, `M05-Grille-sombre-Windows.png` (grid area only) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: Win2D grid drawing
