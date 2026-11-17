# P18-09 - WinUI head: the schema compare view

Status: done
Depends on: P18-08, P9-10

## Goal
Draw M09 in the WinUI head. Recipe 5, 6.

## Design decisions (final)
- `Views/SchemaCompareView.xaml(.cs)` as the tab content. Layout from `M09-Comparaison-*`: header band (`SOURCE` / `CIBLE` chips with engine badge, connection, `database . schema`, environment pill, left stripe in the environment color; swap button; segmented `Schéma` (the `Données` tab is not drawn), `Comparer à nouveau`), left panel 270 wide (chips, filter box, `Tout inclure`, grouped rows, `F7` hint), center two DDL panes with line numbers, `+` / `-` marks, row tints (`cell-added`, `cell-deleted` tokens) and hatched filler, bottom `SCRIPT DE MIGRATION` pane (editable `LoomEditor` read-write if the head already hosts it for SQL, with its highlighting; otherwise the plain editor) with `Copier`, `Enregistrer .sql`, `Ouvrir dans l'éditeur`, the primary (danger when the target is Production) execute button, left stripe in the target environment color.
- Lists are built in code (no object `ItemsSource`, recipe 5). The DDL panes are drawn by one control fed `AlignedLine`s; the two panes scroll together.
- Keys: `F7` / `Maj+F7`, `Ctrl+S` saves the script, `Ctrl+Entrée` executes (still through the confirmation).
- Status bar: environment blocks `STAGING -> PRODUCTION` and the counts text from the view-model.
- Menu item and explorer context item from recipe 2 wired here.

## Out of scope
- Avalonia (P18-10), `Données` tab.

## Tasks
- [x] View, DDL pane control, wiring of menu and context items.
- [x] Tests in `tests/Squill.App.WinUI.Tests` where the harness allows (line count, marks, hatch for filler).

## Acceptance criteria
- Common acceptance (recipe 7) with the WinUI AOT publish; a capture needs two live connections: use two SQLite files (differences created with the P18-08 scenario) and compare with M09 light and dark; list differences; what needs a Production target is "to check by hand".

## Suggested commit message
Add the WinUI schema compare view

## Report
- Built in code like the home page (no `.xaml`): `Views/SchemaCompareView.cs`, `Controls/DdlPane.cs` (Win2D, row tints, `+` / `-` marks, hatched filler, shared scroll), `Views/CompareFormat.cs` (row kinds, tested). The script editor is the head's `EditorFactory` editor (LoomEditor when the build has it).
- Wired: tab host in `MainWindow`, `Base de donnees` menu item with the disabled reason as tooltip, explorer connection context item, status bar (`STAGING -> PRODUCTION`, counts, both server versions), `F7` / `Maj+F7` / `Ctrl+S` / `Ctrl+Entree`.
- Verified: both solutions build with 0 warnings, all tests green, AOT publish without IL2xxx/IL3xxx from our code (third-party SqlClient warnings are the existing ones), published exe starts without a crash log.
- To check by hand (no capture was taken): the M09 light / dark comparison with two live SQLite files, a Production target (danger button, stripe), horizontal scroll of long DDL lines (not implemented, text is clipped).
