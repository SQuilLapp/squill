# P11-21 - WinUI head: explorer glyphs and menu for routines, triggers, definitions

Status: todo
Depends on: P11-20

## Goal
The WinUI explorer draws the new node kinds and offers "Open definition" and "Script as CREATE TABLE" in the context menu.

## Design decisions (final)
- `ExplorerGlyphs` (WinUI `Views`): glyphs for function, procedure, trigger, materialized view (same glyph family and sizes as the existing ones; colours from tokens, nothing hard-coded). Context menu entries bound to the P11-20 commands, labels through `Loc` keys.
- Double click on a view, routine or trigger runs "Open definition"; double click on a table keeps its current action.
- Drive it: with SQLite (trigger + view) and, with containers (P11-01), PostgreSQL, MySQL and SQL Server sample schemas; capture the tree expanded.

## Out of scope
- Avalonia (P11-22).

## Tasks
- [ ] Glyphs, menu, double-click, captures `en` and `fr`.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI AOT publish with zero IL2xxx/IL3xxx warnings.
- To check by hand: connect to the SQLite sample, expand a schema, double-click the view and the trigger, right-click a table and script it; the tabs open with the expected text.

## Suggested commit message
Draw routines and triggers in the WinUI explorer with definition actions
