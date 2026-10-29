# P5-10 - WinUI head: custom-drawn grid on Win2D

Status: todo
Depends on: P5-09

## Goal
The WinUI counterpart of P5-02 with the same budget (risk R4).

## Design decisions (final)
- Package `Microsoft.Graphics.Win2D` (resolve the version compatible with Windows App SDK 2.6.4 via `rtk dotnet package search`; pin it). `Controls/DataGridCanvas` uses `CanvasControl.Draw` (`CanvasDrawingSession`), draws only visible cells with the same layout rules as P5-02 and reuses the pure `GridLayout` (move it to `Squill.Presentation.Results` in this card and adapt the Avalonia head; its tests move with it).
- `CanvasTextFormat`/`CanvasTextLayout` cached in an LRU of 4,096 entries; scrolling through custom `ScrollBar`s like the Avalonia control; invalidate on `IGridDataSource.Changed`.
- `--grid-bench` flag with the same measurement and output as P5-02; append the WinUI numbers to `docs/poc/grid.md`. Budget avg <= 16 ms; if missed STOP and report.

## Out of scope
- Selection/copy (P5-12).

## Tasks
- [ ] Move `GridLayout` and its tests to Presentation; implement the control and bench.
- [ ] Run the bench on the AOT-published exe.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- `docs/poc/grid.md` contains both Avalonia and WinUI numbers.

## Suggested commit message
Add WinUI Win2D virtualized grid and benchmark
