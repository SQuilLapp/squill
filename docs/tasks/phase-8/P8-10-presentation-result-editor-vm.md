# P8-10 - Presentation: result editor view-model

Status: todo
Depends on: P8-05, P8-06, P8-07, P8-09, P4-01

## Goal
The headless view-model behind an editable result grid: edit operations, pending-change state, validation, preview script, commit and discard.

## Design decisions (final)
- Namespace `Squill.Presentation.Editing`. `public sealed partial class ResultEditorViewModel : ViewModelBase` created with `(EditableGridDataSource grid, EditTarget target, ConnectionSession session, IDialogService dialogs, Func<bool> isConnectionBusy)`. Properties (CommunityToolkit `[ObservableProperty]` where they change): `Grid`, `Target`, `HasPendingChanges`, `PendingCount`, `Issues` (`ObservableCollection<EditIssue>`), `StatusText` (`""`, `"3 pending change(s)"`, `"Committed 3 change(s)"`, or the error), `IsCommitting`. They are refreshed after every grid `Changed` event by re-running `ChangeSetBuilder.Build` (cheap: only pending rows are scanned).
- Edit operations (called by the heads through P8-11): `string? TrySetCell(long row, int column, string? text)` parses with `CellValueParser` against the **result** column (`Grid.Columns[column]`), calls `Grid.SetCell` and returns the parse error or null (the grid is unchanged on error); returns `"This column is read-only"` for a column outside the target, `"Rows are still loading"` when `!Grid.CanEdit`, `"Row is marked for deletion"` for a deleted row. `void SetNull(long row, int column)` sets SQL NULL (the only way to store NULL in a text column). `long AddRow()`, `void DeleteRows(IReadOnlyList<long> rows)` (distinct rows, processed from the highest index down so pending-inserted rows do not shift the others), `void RevertRows(IReadOnlyList<long> rows)`.
- Commands: `CommitCommand` (can execute when `HasPendingChanges`, `Issues` is empty and not `IsCommitting`): when `isConnectionBusy()` -> `ShowErrorAsync("Commit blocked", "A query is still running on this connection.")`; when the change set holds deletes -> `ConfirmAsync("Delete rows", "Delete <n> row(s) from <qualified table>? This cannot be undone.")`, declined = nothing happens; run `new EditCommitter(session.Connection, dialect).CommitAsync(target, <the result's ColumnInfo list>, changeSet.Edits, ct)` with a `CancellationTokenSource` (also cancelled by `CancelCommand`); success -> `Grid.AcceptChanges(result.GeneratedKeys)` and `StatusText = "Committed <n> change(s)"`; `EditCommitException` or other provider failure -> `ShowErrorAsync("Commit failed", message)`, the pending changes stay, and `Issues` gets one `EditIssue(row, null, message)` when `Row` is known until the next edit. `DiscardCommand` (confirm `ConfirmAsync("Discard changes", "Discard <n> pending change(s)?")` when any) calls `Grid.DiscardAll()`. `PreviewScriptCommand` builds the statements with `DmlGenerator` and raises event `ScriptRequested(string script)`: statements joined like `DdlGenerators.ToScript`, each followed by one comment line `-- @p0 = <value>, @p1 = <value>` (strings via `SqlQuoting.Literal`, `null` as `NULL`, everything else `CellFormatter`-style text), nothing raised when there are no changes or issues exist.
- `GridEditController` (P8-11) is the only consumer of the edit operations in the heads. No UI types, no reflection.

## Out of scope
- Keyboard behaviour (P8-11), wiring to tabs (P8-12), views.

## Tasks
- [ ] Implement the view-model.
- [ ] Tests in `tests/Squill.Presentation.Tests` (fake connection, fake dialogs as in the designer tests): each `TrySetCell` outcome, `SetNull`, add/delete/revert with pending-inserted rows, `HasPendingChanges`/`PendingCount`/`Issues` refresh, commit success (state cleared, generated key shown), commit blocked while busy, delete confirmation declined and accepted, provider failure keeps the changes and reports the row, discard with and without confirmation, preview script text for each edit kind, cancel.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add result editor view-model
