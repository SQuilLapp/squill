# Multi-table delete verification (P20-21)

Status: **automated done, agent pass minimal (P20-07 WinUI and P20-08 Avalonia: selection and menu text only), manual pending.** The owner ticks the boxes below; the `Result` column holds only what an agent drove.

## Automated (done)

- `tests/Squill.Sql.Tests/TableDropPlanTests.cs` (P20-04, 11 tests): order by foreign keys, chains, diamond, cycle, blockers outside the selection, self reference, MySQL-shaped null schema, case-insensitive match, view rejected, duplicates removed.
- `WorkspaceDropTablesTests` (P20-05, 10 tests plus the unchanged single-drop tests): one flow for one or many tables, production confirmation, session transaction and journal, implicit-commit question, blockers send nothing, partial result message.
- `ExplorerMultiSelectionTests` (P20-06, 10 tests, plus one in `WorkspaceDropTablesTests`): `SelectedTables`, `DropMenuText`, `CanDropMenu`, mixed selections.
- `ExplorerMultiSelectUiTests` (P20-08, 7 headless Avalonia tests): selecting a parent does not add its children; a programmatic selection change keeps the multi-selection. WinUI (P20-07): no automated test of the tree (the control is not instantiable in the head tests).
- `DropTablesTests` (P20-09, 4 tests per engine) on SQLite (default run), PostgreSQL, MySQL and SQL Server (`rtk pwsh eng/run-docker-tests.ps1 -Engine <engine>`, filter `*DropTablesTests`): drop in order, wrong order refused by the engine, blocker, atomicity (all or nothing on SQLite, PostgreSQL and SQL Server; both tables stay dropped on MySQL). Oracle not proved.

## Not driven

The Avalonia multi-select was never driven in the published exe (the window driver has no modifier click). On WinUI the owner drove the selection, `Supprimer 3 tables` and the refusal dialog; no agent saw the `Delete` key, the confirmation dialog, cancel, Production, an open transaction, MySQL, SQLite, the designer tab, the light theme or English.

## How to prepare

- SQLite: `rtk pwsh tools/visual/New-VisualProfile.ps1 -Scenario Full` builds a data directory with `clients`, `commandes` (foreign key to `clients`), other tables and a view; open the connection in each head (`SQUILL_DATA_DIR` as printed). Create a second SQLite profile for the "two connections" row and a third with the environment `Production` on the same file.
- PostgreSQL: `rtk pwsh tools/visual/New-PostgresScenario.ps1 -Action Start`, then a profile with the printed values (type the password in the edit dialog); `-Action Stop` at the end. Create a Production profile on the same database.
- MySQL: `rtk pwsh eng/run-docker-tests.ps1 -Engine mysql -Keep`, then `podman ps -a` shows nothing from this run at the end.
- Start the WinUI head with `SQUILL_LANGUAGE=fr` (then once with `en`), then the Avalonia head. Each row once in the light theme and once in the dark theme.
- Scratch tables for the drop rows: `create table m_a (id integer primary key)`, `create table m_b (id integer primary key)`; for the foreign key rows `clients` and `commandes` of the scenario (work on a copy).

## Checklist

Columns: WinUI / Avalonia = tick when it behaves as described in that head. Result: `ok`, `fixed in <commit>`, `bug -> <card>`, `not driven (why)`; `agent: ...` is an agent pass.

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Select two tables with Ctrl+click, then three with Shift+click: the rows are highlighted, no checkbox, selecting a parent (schema) node adds nothing | [ ] | [ ] | | WinUI: agent (P20-07) could not drive Ctrl+click; the owner saw click, Ctrl+click and Shift+click. Avalonia (P20-08): `not driven`, covered by headless tests |
| 2 Right-click inside the selection: the menu says `Supprimer 2 tables` (English `Delete 2 tables`) and keeps the selection; `Concevoir` and `Générer CREATE TABLE` are disabled | [ ] | [ ] | | WinUI: seen by the owner (`Supprimer 3 tables`). Avalonia: `not driven` |
| 3 With the tree focused press `Delete` on a selection of tables: the confirmation opens | [ ] | [ ] | | `not driven` in both heads |
| 4 Add a view to the selection: the delete entry is disabled | [ ] | [ ] | | |
| 5 Tables of two connections: the entry is disabled (the selection stays inside one connection) | [ ] | [ ] | | |
| 6 The confirmation lists the tables one per line, qualified | [ ] | [ ] | | |
| 7 Cancel the confirmation: nothing is dropped, nothing in the history | [ ] | [ ] | | |
| 8 Drop `clients` and `commandes` together (selected in any order): it works, `commandes` is dropped before `clients` (history shows the order) | [ ] | [ ] | | |
| 9 Select only `clients` while `commandes` stays: a message lists `commandes` as referencing, nothing is sent, both tables remain | [ ] | [ ] | | WinUI: refusal dialog seen by the owner (P20-07) |
| 10 Production profile: only the production dialog appears (statements listed, database name to type, no second confirmation); wrong name keeps `Run` disabled; `Enter` cancels | [ ] | [ ] | | |
| 11 Production, single table through the context menu `Supprimer la table...`: the same production dialog now appears (it did not before phase 20) | [ ] | [ ] | | |
| 12 PostgreSQL in `Manuel` mode with a transaction open: drop two tables, the pill counts the statements, the journal names them; `Annuler` brings both tables back after an explorer reload | [ ] | [ ] | | |
| 13 PostgreSQL / SQL Server / SQLite: make the second drop fail (a blocker created by another client between selection and run): neither table is dropped | [ ] | [ ] | | |
| 14 MySQL with a transaction open: the implicit-commit question appears first; declined sends nothing | [ ] | [ ] | | |
| 15 MySQL: make the second drop fail: the message says which tables were dropped and which failed | [ ] | [ ] | | |
| 16 SQLite file: drop two tables, the file stays usable and the explorer reloads without them | [ ] | [ ] | | |
| 17 A designer tab open on one of the dropped tables closes; other tabs stay | [ ] | [ ] | | |
| 18 The explorer reloads after the drop and the selection is cleared; the tables are gone from completion | [ ] | [ ] | | |
| 19 Light and dark theme: selection fill, the menu and the dialogs are readable | [ ] | [ ] | | WinUI dark: seen by the owner. Light: `not driven` |
| 20 French and English: menu text, dialog texts and the partial-failure message | [ ] | [ ] | | |

## Known limits

- Tables only; views, routines and triggers are not part of it, and there is no `CASCADE` (a table outside the selection that references one blocks the drop).
- MySQL (and Oracle, whose DDL commits implicitly) cannot drop atomically; the flow stops at the first failure and reports. Oracle is not proved.
- A foreign key cycle inside the selection keeps the selection order; the engine's error is reported.
- No selection across connections, no undo, no `Cmd+Backspace` on macOS.
- WinUI explorer rows use a copy of the default `TreeViewItem` template; re-copy it when the Windows App SDK is upgraded.
