# P4-11 - Presentation: result export

Status: todo
Depends on: P4-06

## Goal
Write a complete result set to CSV or JSON.

## Design decisions (final)
- `interface IResultExporter { string FileExtension { get; } ValueTask ExportAsync(IGridDataSource source, Stream destination, CancellationToken ct); }` in `Squill.Presentation.Results`; `CsvResultExporter` (RFC 4180: comma, `\r\n`, quote fields containing `,` `"` CR LF, header row, null as empty, `CellFormatter` text, UTF-8 without BOM) and `JsonResultExporter` (`Utf8JsonWriter`, indented array of objects, native JSON types for numbers/bools, strings otherwise, `null` for null, duplicate column names suffixed `_2`).
- Export reads `source.GetCell` from row 0 to `RowCount` and requires `IsComplete`; otherwise throws `InvalidOperationException`.
- `ResultViewModel.ExportCommand(IResultExporter)` asks `IDialogService.PickSaveFileAsync` and writes the file; errors go to `ShowErrorAsync`.

## Out of scope
- Excel, clipboard export (P5-06).

## Tasks
- [ ] Implement both exporters and the command.
- [ ] Tests: quoting cases, nulls, dates, bytes, duplicate names, 100,000-row export finishes (no per-row allocation assertion, just completion), incomplete source rejected.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add CSV and JSON result export
