# P8-14 - Avalonia head: in-place cell editing in the grid

Status: todo
Depends on: P8-12, P5-06

## Goal
`DataGridCanvas` edits cells in place and shows the state of pending rows.

## Design decisions (final)
- Read `src/Squill.App.Avalonia/Controls/DataGridCanvas.cs` and `DataGridView.cs`. The results panel passes the result's `DisplayGrid`, `Selection` and `EditController` (P8-12) to the canvas through new styled properties (`EditController` may be null: then the canvas behaves exactly as before, read-only).
- Editing UI: while `EditController.IsEditing`, a single `TextBox` (compiled style from the tokens, no hard-coded colours) is positioned over the cell rectangle (re-positioned on scroll, hidden when the cell scrolls out of view) bound two-way to `EditText`; `Error` shown as a red border and a tooltip. Key and mouse mapping is exactly the table in P8-11 (`F2`, double-click, typing starts an edit; Enter, Tab, Shift+Tab, Esc; Delete clears to NULL; Ctrl+V pastes the clipboard text through `Paste`, an error is shown with `IDialogService.ShowErrorAsync`; `Ctrl+Delete`, `Ctrl+Insert`). When no controller is present none of these keys is handled.
- Cell and row drawing (use `GetRowState`, `IsCellModified`, `IsCellInvalid` and `IsColumnEditable` of the controller): `Inserted` rows get a success-tinted background, `Modified` cells an accent-tinted background, `Deleted` rows a danger-tinted background with strikethrough text, a cell with a validation issue a danger border, read-only columns of an editable grid a muted text colour. NULL cells keep the existing `NULL` placeholder. Use existing tokens; if a tint is missing add it to `tokens/squill.tokens.json` and regenerate as described in P5-01.
- Single-click on a cell of an editable grid still only selects (selection logic of P5-06 unchanged).

## Out of scope
- Toolbar and commit buttons (P8-15), WinUI (P8-16), drag-fill, undo.

## Tasks
- [ ] Implement the editing overlay, key handling and drawing.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests` with a fake editable source: F2 shows the text box over the active cell, typing then Enter commits and moves down, Esc cancels, an invalid value keeps the box open with the error, Delete sets NULL, a read-only column does not start an edit, no controller means no editing, rendering does not throw for each `RowState`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings from Squill code.

## Suggested commit message
Add in-place cell editing to the Avalonia grid
