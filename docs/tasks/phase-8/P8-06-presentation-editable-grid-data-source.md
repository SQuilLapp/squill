# P8-06 - Presentation: editable grid data source with pending changes

Status: todo
Depends on: P8-01, P4-06

## Goal
An `IGridDataSource` that wraps a loaded result and records pending cell edits, inserted rows and deleted rows without touching the database or the wrapped data.

## Design decisions (final)
- Namespace `Squill.Presentation.Results`. `public enum RowState { Unchanged, Modified, Inserted, Deleted }` and `public sealed class EditableGridDataSource : IGridDataSource` created with `(IGridDataSource source, EditTarget target)`. It forwards `Columns`, `IsComplete`, `IsTruncated` and re-raises the source's `Changed` event.
- `public bool CanEdit => source.IsComplete;` every mutating method throws `InvalidOperationException` when `CanEdit` is false (rows are still streaming, so indices are not stable). Only columns with `target.IsEditable(ordinal)` can be set; others throw `ArgumentException`.
- Internal state (this is the design, implement it as such so memory stays small for 1M rows): `_changes: Dictionary<(long BaseRow, int Column), object?>` of pending cell values keyed by **base** row; `_deleted: HashSet<long>` of base rows pending deletion; `_removed: List<long>` sorted base rows whose deletion was committed (hidden); `_extra: List<ExtraRow>` for inserted rows (`object?[] Values`, `bool[] IsSet`, `bool IsPending`; a committed insert stays in `_extra` with `IsPending = false`); `_committed: Dictionary<(long, int), object?>` values written by a committed update (these replace the source value as the new "original"). Visible row `r` maps to base row by skipping `_removed` (binary search); rows `>= baseVisibleCount` are `_extra[r - baseVisibleCount]`.
- `RowCount = source.RowCount - _removed.Count + _extra.Count`. `GetCell(row, column)` returns, in order: the pending change, the committed value, the source value; for extra rows the stored value; `null` outside the range.
- Read API: `RowState GetRowState(long row)` (a pending-inserted row is `Inserted`, a committed-inserted row `Unchanged`, a base row with any pending change `Modified`, in `_deleted` `Deleted`), `bool IsCellModified(long row, int column)`, `object? GetOriginal(long row, int column)` (committed or source value of a base row; `null` for extra rows), `bool IsCellSet(long row, int column)` (inserted rows: whether the user gave the cell a value), `bool HasPendingChanges`, `int PendingCount` (rows whose state is not `Unchanged`), `bool IsPersistedRow(long row)` (false for a pending-inserted row).
- Write API: `void SetCell(long row, int column, object? value)`: on a base row stores the change, but **removes it again when the value equals the original** (`object.Equals`, `byte[]` compared with `SequenceEqual`); on a pending-inserted row stores the value and marks it set; on a deleted row throws `InvalidOperationException`; a committed-inserted row behaves like a base row (the original is its stored value). `long AddRow()` appends a pending-inserted row (all cells `null`, none set) and returns its index. `void DeleteRow(long row)`: a base row becomes `Deleted` (stays visible, its pending cell changes are dropped); a pending-inserted row is removed outright (later row indices shift down by one). `void RevertRow(long row)`: drops the changes and the deleted flag of a base row, or removes a pending-inserted row. `void DiscardAll()`.
- `void AcceptChanges(IReadOnlyDictionary<long, object?> generatedKeys)` after a successful commit: pending cell changes move to `_committed`, deleted rows move to `_removed` (so they disappear and later indices shift), pending-inserted rows get `IsPending = false`; `generatedKeys` maps the **grid row index before the call** of an inserted row to the value of its generated key column, which is stored in that row (the key column is `target.KeyColumns[0]`'s ordinal). Indices of the keys are translated before any row is removed. Raises `Changed` once.
- Every mutating method raises `Changed` (synchronously, on the caller's thread, which is the UI thread). Not thread-safe; documented as UI-thread only.

## Out of scope
- Parsing text, validation, SQL (P8-05, P8-07, P8-09), any UI.

## Tasks
- [ ] Implement `RowState` and `EditableGridDataSource`.
- [ ] Tests in `tests/Squill.Presentation.Tests` with a fake `IGridDataSource` (use `SyntheticGridSource` or a small array-backed fake): edit, edit back to the original clears the change, edit two cells of a row, add and edit an inserted row, delete a base row and a pending-inserted row (index shift), revert, discard all, `AcceptChanges` for updates (original changes), deletes (rows vanish, later rows shift) and inserts with a generated key, `CanEdit` false while the source is incomplete (throws), read-only column throws, `byte[]` equality, `Changed` raised per mutation, `RowCount` arithmetic, a 1,000,000-row `SyntheticGridSource` with 10 edits stays cheap (no per-row allocation: assert `GetCell` for an untouched row does not allocate beyond the source value).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add editable grid data source with pending changes
