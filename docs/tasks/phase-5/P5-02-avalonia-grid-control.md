# P5-02 - Avalonia head: custom-drawn virtualized grid

Status: todo
Depends on: P5-01

## Goal
A grid control that draws only visible cells and stays smooth on 1,000,000 rows (risk R4).

## Design decisions (final)
- `Controls/DataGridCanvas.cs` in the Avalonia head: derives from `Control`, `DataSource` styled property (`IGridDataSource`), overrides `Render(DrawingContext)` drawing only visible rows and columns (row height and fonts from the token resources; header row fixed), custom vertical and horizontal `ScrollBar`s hosted in a `Grid` wrapper `DataGridView`; column widths = max(header text width, sample of first 100 rows) clamped 60-400 px, user resizable by dragging header borders.
- No per-cell control, no per-row object. `FormattedText` is cached per (text, brush) in a small LRU (4,096 entries). `NULL` is drawn in the muted token color. Re-render on `IGridDataSource.Changed` (invalidate only).
- `--grid-bench` command-line flag on the app: builds a synthetic 1,000,000 x 12 `IGridDataSource` (ints, strings, dates, 10% nulls), opens a window with the grid, scrolls 300 frames by 3 rows each from `DispatcherTimer`/`TopLevel.RequestAnimationFrame`, prints `avg frame ms`, `p95 frame ms` and exits. Budget: avg <= 16 ms. Record the numbers in `docs/poc/grid.md`; if missed, STOP and report.
- Headless unit tests (`tests/Squill.App.Avalonia.Tests`, `Avalonia.Headless`; if its xunit package does not support xunit.v3, drive `HeadlessUnitTestSession` manually) check hit-testing helpers (`GridLayout` pure class: visible row range for scroll offset, column at x) which must be separate from the control and covered thoroughly.

## Out of scope
- Selection, copy (P5-06).
- Sorting.

## Tasks
- [ ] Create `GridLayout` (pure), the control, the wrapper, the bench flag; add the test project.
- [ ] Run the bench against the AOT-published exe and write `docs/poc/grid.md`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- `docs/poc/grid.md` contains the measured Avalonia numbers.
- The bench process exits by itself; nothing left running.

## Suggested commit message
Add Avalonia custom-drawn virtualized grid and benchmark
