# P8-01 - Core: row edit model

Status: done
Depends on: P7-01

## Goal
The dialect-independent vocabulary of phase 8: which table a result grid edits, and the row edits (insert, update, delete) a commit consists of. Pure data, no behaviour.

## Design decisions (final)
- Namespace `Squill.Core.Editing`, folder `src/Squill.Core/Editing/`, one file per type, all `public sealed record` (or `abstract record`) with XML doc.
- `ColumnValue(string Column, object? Value)`: a table column name (never an alias) and its CLR value; SQL NULL is `null`.
- `EditColumn(int ResultOrdinal, ColumnDefinition Definition)`: a result column that maps one-to-one to a table column (`ResultOrdinal` is `ColumnInfo.Ordinal`).
- `EditTarget(string? Schema, string Table, IReadOnlyList<EditColumn> Columns, IReadOnlyList<string> KeyColumns, TableInfo Info)`: the table being edited. `Columns` are only the result columns that map to table columns (result columns that do not, such as computed ones, are read-only and absent). `KeyColumns` are the primary-key column names in key order; every one of them is in `Columns`. `Info` is the schema snapshot entry (used by generators for column types and auto-increment flags). Helpers `EditColumn? ByOrdinal(int resultOrdinal)` and `bool IsEditable(int resultOrdinal)`.
- `abstract record RowEdit;` with `InsertRow(IReadOnlyList<ColumnValue> Values)` (only the columns the user filled or that have no default; omitted columns take the database default), `UpdateRow(IReadOnlyList<ColumnValue> Key, IReadOnlyList<ColumnValue> Changes)` (`Key` holds the **original** values of the key columns in key order, `Changes` only the modified columns with their new values; key columns may appear in `Changes`) and `DeleteRow(IReadOnlyList<ColumnValue> Key)`.
- `EditIssue(long Row, string? Column, string Message)`: a validation error on a grid row (`Row` is the grid row index, `Column` the table column name or null for a whole row).
- No behaviour beyond the two helpers above; no reflection.

## Out of scope
- Eligibility analysis (P8-02), SQL generation (P8-03), the pending-change store (P8-06).

## Tasks
- [x] Implement the records and helpers.
- [x] Tests in `tests/Squill.Core.Tests`: `ByOrdinal` and `IsEditable` for mapped, unmapped and out-of-range ordinals; value equality of the records.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add row edit model to Core
