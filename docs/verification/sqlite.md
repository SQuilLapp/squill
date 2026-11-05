# Verification report: SQLite

Card: [P11-36](../tasks/phase-11/P11-36-verify-sqlite.md). Status: **automated part done, manual part pending the owner** (nobody could drive the WinUI window from an automated session).

Legend: tick a box when the step gives the expected result. If it does not, write `bug` and a one-line note under the step; the owner then asks for a card.

## 1. Automated (done, runs in `dotnet test`, never skipped)

These ran through the real `QueryTabViewModel`, `ResultEditorViewModel`, `SessionManager` and the SQLite provider on a real file, which is everything the window does except drawing.

| Area | Test | Result |
|---|---|---|
| Several statements, Run All, run under the caret, rows-affected message, history entries | `QueryFlowTests` (`tests/Shared/QueryFlowScenario.cs`) | ok, fixed in this card (rows-affected text) |
| A failing statement: error text in a result tab, later statements not run, history marks it failed | `QueryFlowTests` | ok |
| Parameters: `:name` prompt and value bound, `?` refused with a message | `QueryFlowTests` | ok |
| Cancel a long statement: status "cancelled", tab and connection still usable | `QueryFlowTests` | ok, fixed in this card (UI thread was blocked) |
| 100,000 rows stream completely, export CSV and JSON of them | `QueryFlowTests` | ok |
| Completion on a table created in the session (after schema refresh) | `QueryFlowTests` | ok |
| Grid editing: update by key and alias, key column, composite key, insert, delete, read-only sources, failing commit, trigger table, typed keys | `GridEditEndToEndTests` | ok (P11-33) |
| Table designer: create, alter (rebuild), failure | `DesignerMatrixTests`, `SqliteAlterExecutionTests` | ok (P11-30) |
| ER diagram on the stress schema | `ErStressTests` | ok (P11-31) |
| Column types read and written | `ColumnTypeReadTests`, `ColumnTypeEditTests` | ok (P11-25) |
| Export snapshots | `ExportSnapshotScenario` | ok (P11-32) |
| Schema objects: tables, views, indexes, FKs, triggers | `SqliteSchemaReaderTests`, `SqliteSchemaObjectsTests` | ok |

Two real bugs found by the new scenario and fixed (both with the scenario as regression test):

1. `QueryTabViewModel` read `RecordsAffected` before draining the reader, so every INSERT/UPDATE/DELETE said "Command completed" instead of "N rows affected" on all engines.
2. `SqliteInterrupt` ran the first step of a statement synchronously on the caller's thread: a slow statement froze the UI thread and the Cancel button could not be used.

## 2. Setup for the manual part

```bash
# English UI (the checks below name the English labels; repeat the connection dialog once in French)
$env:SQUILL_LANGUAGE = 'en'
dotnet run --project src/Squill.App.WinUI -c Release
```

Make a scratch database: `sqlite3 %TEMP%\squill-verify.db < tests\Shared\SampleSchema\sqlite.sql` (or let the app create an empty file and run the script from a query tab). Avalonia head: `dotnet run --project src/Squill.App.Avalonia -c Release`.

## 3. Manual checklist

Columns: **W** = WinUI (required), **A** = Avalonia (smoke rows only, marked *smoke*).

### Connections
- [ ] W - New connection, engine SQLite: the file picker button opens a dialog and fills the path. Save with an empty path: a validation message appears (English). Switch language to French (Settings) and repeat: the message is French.
- [ ] W - Edit the connection (rename), save, delete it (asks for confirmation).
- [ ] W / A *smoke* - Connect to the scratch file: explorer shows tables, views, indexes, foreign keys, triggers with counts. Disconnect, reconnect.
- [ ] W - Explorer refresh (button): a table created from another tool appears.

### Query run
- [ ] W / A *smoke* - Open table `customers` (double click): rows appear.
- [ ] W - F5 on `select 1; select 2` with the caret in the first statement: one result. Run All (Ctrl+Enter or the button): two results.
- [ ] W - `select * from customers where name = :n`: a parameter dialog asks for `n`; value `x` gives zero rows. `select ?`: message that positional parameters are not supported.
- [ ] W - Long statement: `with recursive c(n) as (select 1 union all select n+1 from c where n<2000000000) select count(*) from c`; F5, then Esc or Cancel after 2 s: status says cancelled and the window stayed responsive while it ran.
- [ ] W - `select * from missing`: error text in a result tab, caret/marker on the statement.

### Result grid
- [ ] W / A *smoke* - `with recursive c(n) as (select 1 union all select n+1 from c where n<100000) select n from c`: scrolling to the end is smooth, header shows 100,000 rows.
- [ ] W - Select cells, Ctrl+C, paste into a text editor: values tab/newline separated.
- [ ] W - Export CSV and JSON through the file dialog, open the files: 100,001 CSV lines, valid JSON.

### Grid editing
- [ ] W / A *smoke* - `select * from customers`: edit a cell, add a row, delete a row; the pending counter shows; Discard clears; Commit writes (re-run the select to check).
- [ ] W - Edit the key column of a row; a read-only source (join or view) shows the reason; a statement with a failing constraint shows the error and keeps the edits.
- [ ] W - The issues popup lists a missing NOT NULL value before commit.
- [ ] W - Close the tab with uncommitted edits: it asks.

### Table designer
- [ ] W / A *smoke* - New table with two columns, preview SQL, apply; it appears in the explorer.
- [ ] W - Alter it (add a column, change a type): the preview shows the rebuild; apply; a deliberately invalid change shows the failure message.

### ER diagram
- [ ] W / A *smoke* - Open the ER diagram of the database: tables and relations drawn; drag a table; save layout; reopen: layout kept. Export SVG and open it in a browser.

### Editor
- [ ] W - Completion lists `customers` after `from `; hover on a column shows its type; a typo in a table name gets a diagnostic underline; Ctrl+Shift+F formats and keeps the caret near its place.

### History, snippets, settings
- [ ] W - History panel: re-run an entry, search for a word, delete an entry. Snippets: save the selection, insert it in a new tab.
- [ ] W - Settings: theme light/dark/system applies at once; language change shows the "restart" text; formatter keyword case changes the output of the format command.

### Terminal and chrome
- [ ] W - Terminal tab: without `sqlite3` on PATH a clear message is shown (rename it temporarily, or test on a machine without it); with it, the shell opens on the file.
- [ ] W - Menu, left rail, status bar are consistent; closing a query tab with unsaved text asks; dialogs are centred and Esc closes them.

Result of the manual part (owner): ______ (date) - rows that failed: ______
