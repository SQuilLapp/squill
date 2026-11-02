# P7-24 - Avalonia head: table designer view

Status: todo
Depends on: P7-23, P7-21

## Goal
The editing UI for `TableDesignerViewModel` on the Avalonia head, replacing the placeholder of P7-23.

## Design decisions (final)
- `Views/TableDesignerView` (`UserControl`, compiled bindings, tokens only, no hard-coded colours): header row with `Schema` and `Name` text boxes and the buttons `Apply`, `Revert`, `Open script`, `Cancel`; below, a `TabControl` with `Columns`, `Indexes`, `Foreign keys`; at the bottom a split area with a read-only monospaced script preview (`Script`, selectable, plain `TextBox`, no AvaloniaEdit needed) and the `Issues` list (error / warning glyph, message) plus `StatusText`.
- Columns tab: an `ItemsControl` of rows (no DataGrid package): `Name` text box, `DataType` editable `AutoCompleteBox` over `TypeSuggestions` (free text allowed), `Nullable`, `PK`, `Auto` check boxes, `Default` text box, row buttons up / down / delete; an `Add column` button. Indexes tab: name, `Unique`, `Columns` (comma separated), delete; foreign keys tab: name, columns, referenced schema, referenced table, referenced columns, `ON DELETE` and `ON UPDATE` combo boxes over `ReferentialActions`, delete. Rows reflect view-model validation by a subtle error border on the row owning an issue (match `DesignIssue.Subject`).
- Shell: render `Designers` as tabs (replace the placeholder of P7-23) with a close button running `CloseDesignerCommand`; `New table` button in the sidebar toolbar bound to `NewTableCommand`.
- Keyboard: `Ctrl+S` applies the active designer, `Ctrl+Z` is not handled (the text boxes keep their own undo).

## Out of scope
- WinUI (P7-26), drag-and-drop reordering (use the up/down buttons), column type pickers beyond the suggestion list.

## Tasks
- [ ] Implement the view and the shell changes.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests`: adding a column row updates the script text, a validation error shows in the issues list and disables Apply, `Ctrl+S` runs the apply command with a fake connection, the foreign key tab binds.
- [ ] Run the AOT-published head on a temp SQLite file: create a table through the designer, rename a column, apply, check the schema with a query. State in the card report what was and was not exercised (no UI automation may mean only startup was verified). Close every process you started.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings from Squill code.
- No process left running.

## Suggested commit message
Add table designer view to the Avalonia head
