# P4-06 - Presentation: streaming grid data source and cell formatting

Status: todo
Depends on: P4-04

## Goal
Feed result rows to a virtualizing grid without copying them (risk R4, data side).

## Design decisions (final)
- Namespace `Squill.Presentation.Results`. `interface IGridDataSource { IReadOnlyList<ColumnInfo> Columns { get; } long RowCount { get; } bool IsComplete { get; } bool IsTruncated { get; } object? GetCell(long row, int column); event EventHandler? Changed; }`.
- `StreamingGridDataSource(IResultStream stream, int maxRows, IUiDispatcher dispatcher)`: `StartAsync(CancellationToken)` reads batches of 1,000 rows from the stream and appends the returned `object?[]` row arrays into chunks of 4,096 rows (`List<object?[][]>`, row arrays are stored as given, never copied); `RowCount` is updated before `Changed` is raised via the dispatcher, throttled to at most one event per 50 ms plus a final one; stops at `maxRows` setting `IsTruncated`; `GetCell` is thread-safe for readers (chunk list append is the only mutation, guarded by a lock) and returns `null` outside the loaded range.
- `CellFormatter.Format(object? value, ColumnInfo column)` returns `null` for null; invariant culture; `DateTime`/`DateTimeOffset` ISO 8601 (`yyyy-MM-dd HH:mm:ss[.fff]`, offset kept); `bool` `true`/`false`; `byte[]` as `0x` + up to 32 bytes of hex plus `…` when longer; everything else `ToString()`. The view shows `NULL` itself.

## Out of scope
- Sorting/filtering of results.
- The control (P5-02).

## Tasks
- [ ] Implement the data source and formatter.
- [ ] Tests with a fake stream: 10,000 rows across batches, truncation at `maxRows`, cancellation mid-stream, `Changed` throttling (use a fake clock-free assertion: count <= rows/1000 + 2), formatter table.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add streaming grid data source and cell formatter
