# P8-07 - Presentation: change set and validation

Status: todo
Depends on: P8-06, P8-01

## Goal
Turn the pending state of an `EditableGridDataSource` into an ordered list of `RowEdit`s and report validation errors before anything is sent to the database.

## Design decisions (final)
- Namespace `Squill.Presentation.Editing`, folder `src/Squill.Presentation/Editing/`. `public sealed record PendingEdit(long Row, RowEdit Edit)` and `public sealed record ChangeSet(IReadOnlyList<PendingEdit> Edits, IReadOnlyList<EditIssue> Issues)` and `public static class ChangeSetBuilder { public static ChangeSet Build(EditableGridDataSource grid, EditTarget target); }`. Pure and synchronous; it only reads the grid.
- Edit order (also the execution order, P8-09): all **deletes** (grid order), then **updates** (grid order), then **inserts** (grid order). A pending row has exactly one edit.
- Delete: `DeleteRow(Key)` with the key columns' original values (`GetOriginal`). Update: `UpdateRow(Key, Changes)` where `Key` = original key values and `Changes` = the modified cells in column order with table column names (`EditColumn.Definition.Name`). Insert: `InsertRow(Values)` with every cell the user set (`IsCellSet`), in column order; cells never set are omitted (database default).
- Validation, one `EditIssue(row, columnName, message)` each, all collected (do not stop at the first): a modified or set cell that is `null` in a column that is not nullable -> `<column> cannot be NULL`; an inserted row where a column is not nullable, has no default, is not auto-increment and was never set -> `<column> is required`; two pending inserts with identical explicit values for all key columns -> `duplicate key in pending rows` (reported on the later row). Clashes with existing rows, or between updates, are **not** checked: the database reports them at commit.
- Messages are plain English and name the column with the table's spelling.

## Out of scope
- Type conversion (done when the cell is set, P8-10), SQL generation and execution (P8-03, P8-09), constraint checks that need the database (unique, foreign keys, check): the database reports them at commit and P8-09 maps the failure to the row.

## Tasks
- [ ] Implement the builder and records.
- [ ] Tests in `tests/Squill.Presentation.Tests`: edit order, one update merging several cells, update of a key column keeps the original key, delete uses original key, insert omits unset columns, each validation message, several issues at once, no pending changes gives empty lists, a row edited back to its original yields no edit.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add change set builder and validation for grid edits
