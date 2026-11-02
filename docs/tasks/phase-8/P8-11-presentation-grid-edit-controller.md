# P8-11 - Presentation: grid edit controller (shared keyboard and cell-edit logic)

Status: todo
Depends on: P8-10, P5-06

## Goal
One framework-free state machine that both heads drive to edit cells, so the editing rules (begin, commit, cancel, move, clear, paste) are written and tested once.

## Design decisions (final)
- Namespace `Squill.Presentation.Editing`. `public sealed class GridEditController(ResultEditorViewModel editor, GridSelection selection)` (`GridSelection` is the pure selection class of P5-06, read it). Properties: `bool IsEditing`, `long EditRow`, `int EditColumn`, `string EditText`, `string? Error`; event `EditStateChanged` (raised after every change of the properties).
- `bool Begin(string? initialText = null)`: starts editing the active cell of the selection when the grid `CanEdit`, the column is editable, and the row is not `Deleted`; `initialText == null` starts with the cell's current formatted text (`CellFormatter`, `NULL` cell = empty text), a non-null text replaces it (typing a character on a selected cell starts the edit with that character). Returns false otherwise.
- `void SetText(string text)` updates `EditText` and clears `Error`. `bool Commit(EditMove move)` with `enum EditMove { None, Down, Right, Left }`: calls `editor.TrySetCell(EditRow, EditColumn, EditText)`; on an error sets `Error`, keeps editing and returns false; on success ends the edit, moves the selection with `selection.Move` (Down = row + 1, Right = column + 1, Left = column - 1, clamped) and returns true. `void Cancel()` ends the edit without changes.
- Keys the heads map onto it (documented here so both heads behave the same): `F2` or double-click = `Begin()`; a printable character on a selected cell = `Begin(character)`; `Enter` = `Commit(Down)`; `Tab` = `Commit(Right)`; `Shift+Tab` = `Commit(Left)`; `Esc` = `Cancel()`; `Delete` outside edit mode = `ClearSelection()`; `Ctrl+Insert`/`Ctrl+Shift+N` = `AddRow()`; `Ctrl+Delete` = `DeleteSelectedRows()`; `Ctrl+Z` is not handled (use revert of the row, below); `Ctrl+V` = `Paste(text)`.
- `void ClearSelection()`: sets SQL NULL (`editor.SetNull`) in every editable cell of the selection; read-only and deleted-row cells are skipped. `long AddRow()`: `editor.AddRow()`, selects its first editable cell and begins no edit. `void DeleteSelectedRows()`: rows covered by the selection (the active row for a single cell or a column selection) -> `editor.DeleteRows`. `void RevertSelectedRows()` -> `editor.RevertRows`.
- `string? Paste(string text)`: parses TSV (`\r\n` or `\n` rows, `\t` columns, one trailing empty line ignored) and writes the block starting at the active cell through `TrySetCell`, skipping read-only columns, stopping at the last row and last column (no rows are created); returns the first error (`"Row <n>, column <name>: <message>"`) or null; cells before the error stay set. Empty text in a string column pastes the empty string, `\N` pastes NULL.
- Read accessors for the heads' drawing: `RowState GetRowState(long row)`, `bool IsCellModified(long row, int column)`, `bool IsCellInvalid(long row, int column)` (an `EditIssue` of the editor matches the row and the column's table name, or has no column), `bool IsColumnEditable(int column)`, all delegating to the editor and its grid.
- A new selection or a data change while editing ends the edit with `Cancel()`. No UI types.

## Out of scope
- Drawing the editor, focus handling, clipboard access (heads), undo stack.

## Tasks
- [ ] Implement the controller.
- [ ] Tests in `tests/Squill.Presentation.Tests`: begin on editable, read-only, deleted and still-loading cases, typed first character, commit moves (down, right, left, clamped at the edges), commit error keeps editing, cancel, clear sets NULL only on editable cells, add row selects the first editable cell, delete and revert on a multi-row selection, read accessors, paste block (inside bounds, overflowing, with an invalid value, `\N`).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add grid edit controller
