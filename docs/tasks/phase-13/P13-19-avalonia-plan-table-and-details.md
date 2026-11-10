# P13-19 - Avalonia head: plan table, details panel and warnings

Status: todo
Depends on: P13-18, P13-16

## Goal
Twin of P13-16 for Avalonia: the `Arbre` view (table + details + warning box + SQL Server index suggestion), also the lower half of `Graphique`.

## Design decisions (final)
- Same columns, row height 28, indentation, cost bars (96 x 4, color by severity), collapsed columns when no row has the figure, selection two-way with `Plan.SelectedRow`, details panel 264 wide (title + percent, properties list, amber warning box, `Suggestion d'index` card with `Copier` and `Ouvrir dans l'éditeur`), splitter at about 62 % in `Graphique`: exactly as in P13-16 (read it). Use a `ListBox`/`ItemsControl` with a data template (compiled bindings, no reflection bindings: the Avalonia head is AOT).
- Clipboard through Avalonia's `TopLevel.Clipboard`; opening in the editor through the same workspace `ScriptRequested` path as P13-16.

## Out of scope
- Graph (P13-20), WinUI.

## Tasks
- [ ] Table, details, suggestion card, bindings, strings (`plan.copy`, `plan.openineditor` already added by P13-16: reuse).
- [ ] Capture `Arbre` light and dark with a PostgreSQL ANALYZE plan and a SQLite plan; compare with M06; report differences.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green; Avalonia AOT publish zero IL2xxx/IL3xxx.
- To check by hand: row selection, warning box, `Copier`.

## Suggested commit message
Show the plan table, node details and index suggestion in the Avalonia head
