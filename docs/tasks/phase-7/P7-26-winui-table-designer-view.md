# P7-26 - WinUI head: table designer view

Status: done
Depends on: P7-25, P7-21

## Goal
The WinUI counterpart of P7-24.

## Design decisions (final)
- `Views/TableDesignerView` with the same structure and bindings as P7-24 (`x:Bind`): header with schema/name and the buttons, `TabView`/`Pivot` with `Columns`, `Indexes`, `Foreign keys`, script preview (read-only `TextBox` with a monospaced font from the tokens) and issues list. Rows are `ItemsControl`/`ItemsRepeater` templates (no extra package): `AutoSuggestBox` for the type (free text allowed, suggestions from `TypeSuggestions`), check boxes, combo boxes for the referential actions, up / down / delete buttons.
- Replace the placeholder of P7-25 with this view for `Designers`; `New table` button bound to `NewTableCommand`; `Ctrl+S` applies the active designer.
- Row error styling through the same `DesignIssue.Subject` matching as P7-24.
- Build and test with `Squill.Windows.slnx`.

## Out of scope
- Anything in the Avalonia head, drag-and-drop reordering.

## Tasks
- [x] Implement the view and the shell changes.
- [x] Tests in `tests/Squill.App.WinUI.Tests` for the testable parts; run the AOT-published head on a temp SQLite file, create a table with the designer if the session allows UI driving, otherwise verify startup only. State in the card report what was and was not exercised. Close every process you started.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- No process left running.

## Suggested commit message
Add table designer view to the WinUI head
