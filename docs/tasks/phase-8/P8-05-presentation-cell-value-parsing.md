# P8-05 - Presentation: parsing edited cell text into typed values

Status: todo
Depends on: P4-06

## Goal
Convert the text a user types into a cell into the CLR value passed as a statement parameter, with a clear error otherwise. The inverse of `CellFormatter`.

## Design decisions (final)
- Namespace `Squill.Presentation.Results`, `public static class CellValueParser { public static bool TryParse(string? text, ColumnInfo column, out object? value, out string? error); }`. Pure, invariant culture, no reflection.
- `text == null` always gives `value = null` (SQL NULL). For a column whose `ClrType` is not `string`, empty or whitespace-only text also means NULL. For a `string` column empty text is the empty string (NULL is set explicitly by the UI, see P8-10). The nullability check belongs to P8-07, not here.
- Supported by `ColumnInfo.ClrType`: `string`; `bool` (`true`/`false`/`1`/`0`/`yes`/`no`, case-insensitive); `byte`, `short`, `int`, `long`, `sbyte`, `ushort`, `uint`, `ulong` (`NumberStyles.Integer`); `float`, `double`, `decimal` (`NumberStyles.Float`, `.` as separator); `DateTime` (ISO 8601 as written by `CellFormatter`, also date only), `DateTimeOffset` (ISO 8601 with offset; without offset: error), `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`; `byte[]` (`0x` + hex, even digit count, or empty). Any other `ClrType` (including `object`): the text is passed as the `string` itself (the provider converts), no error.
- Errors are short user-facing English: `"<text>" is not a valid <friendly type> value` with friendly types `integer`, `number`, `true/false`, `date and time`, `date`, `time`, `GUID`, `binary (0x...)`; out-of-range integers say `"<text>" is out of range for <type>`.
- Round trip to test: for every supported type, `TryParse(CellFormatter.Format(v, col), col)` returns `v`. The truncated binary display (ending in `…`) is rejected as invalid.

## Out of scope
- Provider-specific types (arrays, JSON, geometry, intervals): passed as text.
- Any UI.

## Tasks
- [ ] Implement `CellValueParser`.
- [ ] Tests in `tests/Squill.Presentation.Tests`: a table per type (valid, invalid, boundary, null/empty rules) and the round trip with `CellFormatter`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add cell value parser for grid editing
