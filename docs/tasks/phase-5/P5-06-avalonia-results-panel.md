# P5-06 - Avalonia head: results panel, selection, copy and export

Status: done
Depends on: P5-05, P4-11

## Goal
Complete the results area.

## Design decisions (final)
- Results tab strip below the editor: one tab per `ResultViewModel` (`Result N`, `Messages`), grid or message text as content; empty and running states (progress bar from `IsRunning`), row count + `truncated` marker in the tab header.
- Grid selection in `DataGridCanvas`: click selects a cell, Shift+click a rectangle, click on a column header selects the column, Ctrl+A everything loaded; Ctrl+C copies the selection as TSV through `CellFormatter` (nulls as empty), with header row when whole columns are selected; keyboard arrows/PageUp/PageDown/Home/End move the active cell and scroll it into view. Selection logic lives in a pure `GridSelection` class with unit tests.
- Export button with CSV/JSON choice calling `ResultViewModel.ExportCommand`.

## Out of scope
- Cell editing, sorting, filtering.

## Tasks
- [x] Implement the panel, `GridSelection`, copy and export wiring.
- [x] Tests: `GridSelection` rectangle/column/all/extend/move; TSV text for a small data source.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add Avalonia results panel with selection, copy and export
