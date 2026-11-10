# P13-16 - WinUI head: plan table, details panel and warnings

Status: todo
Depends on: P13-15

## Goal
The `Arbre` view of the WinUI plan tab: the table of nodes with relative-cost bars and the details panel with the warning box and the SQL Server index suggestion. The table is the lower half of the `Graphique` view too.

## Design decisions (final)
- `Views/PlanTable` (new, same file area as the tab view; a `ListView`/`ItemsRepeater` with a fixed row height of 28 is enough, rows are text, no virtualized custom grid needed; reuse existing styles from P9-06): header row `Nœud | Coût relatif | Coût total | Lignes est. | Lignes réelles | Temps | Boucles`, row = indent `Depth * 14` + operation semibold + target mono `fg-3`; `Coût relatif` = a 96 x 4 bar (track `bg-active`, fill color by severity: normal `fg-3`, alert `warning`, critical `danger`) + the percent right-aligned; numbers right-aligned in mono; the `Lignes réelles` cell is amber when `MisestimateText` is set and shows the cell value only (factor goes in the graph card and details); selected row `selection`; helper rows shown in `fg-3`. Selection is two-way with `Plan.SelectedRow` (the graph selects the same row). Columns for figures that no row has (SQLite: everything but the node) are collapsed; the node column takes the width.
- Details panel (264 wide, `bg-surface`, left border): title = `Details.Title` + the percent at the right in the severity color, a red/amber 8 px square before the title; a two-column list `Details.Properties` (label `fg-2` left, value mono right, wrap); warning box (amber `warning-soft`, warning triangle icon, text 13) for each node warning; below, when `Details.Suggestion` exists, a card (`accent-soft` border like the mockup) `Suggestion d'index` with the `CREATE INDEX` in a mono block with the `syn-*` keyword color and two buttons `Copier` (clipboard) and `Ouvrir dans l'éditeur` (raises the existing `ScriptRequested` path of the workspace to open the text in a new query tab on the same session; look at how P8-12 opens DML scripts).
- Splitter between canvas area and table at about 62 % of the body (mockup: table starts at y 480 of 756) in `Graphique`; in `Arbre` the table takes the whole body; `Texte` unchanged.

## Out of scope
- The graph (P13-17), column sorting, node collapse, Avalonia.

## Tasks
- [ ] Table, details panel, suggestion card, bindings, localized strings reuse of P13-12/14 keys (add `plan.copy`, `plan.openineditor` fr/en).
- [ ] Capture `Arbre` and `Graphique` (table half) light and dark with the PostgreSQL plan of the P11 sample schema (container via `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres`'s fixture is not interactive: instead start `postgres:17-alpine` with the Podman steps of `docs/verification/postgres.md`, close it afterwards) and compare with the PNGs; SQLite plan captured too (no bars, no figures).

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI AOT publish zero IL2xxx/IL3xxx.
- To check by hand: select a row with a misestimate, a sort spill and (SQL Server) a missing index; the details and suggestion show; `Copier` puts the `CREATE INDEX` on the clipboard.

## Suggested commit message
Show the plan table, node details and index suggestion in the WinUI head
