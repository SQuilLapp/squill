# Verification report: MySQL

Card: [P11-38](../tasks/phase-11/P11-38-verify-mysql.md). Status: **automated part done, manual part pending the owner** (no automated session can drive the WinUI window).

## 1. Automated (done)

Run with `pwsh eng/run-docker-tests.ps1 -Engine mysql` (82 tests, all green, container removed afterwards).

| Area | Test | Result |
|---|---|---|
| Query tab flow on a live server: several statements, run under the caret, failure stops the script, `@name` parameter prompt and bound value, `?` refused, `sleep(30)` cancelled, 100,000 rows, CSV and JSON export, completion | `QueryFlowTests` | ok |
| `ON DUPLICATE KEY UPDATE`: runs, affected count 1 then 2 (MySQL's convention), value updated | `MySqlVerificationTests` | ok |
| Zero dates (`0000-00-00`) are read without failing | `MySqlVerificationTests` | ok |
| Wrong password: readable "access denied" message | `MySqlVerificationTests` | ok |
| Table-name case as stored (`lower_case_table_names`), backtick quoting of `odd table` in completion | `MySqlVerificationTests` | ok |
| `CALL` with two result sets | `MultiResultTests` | ok (P11-11) |
| TLS require / verify-full, command timeout, second database listing (database = schema) | `MySqlDatabaseProviderTests` | ok |
| Schema, routines, triggers and their definitions, views, FKs | `MySqlSchemaReaderTests` | ok |
| `tinyint(1)`, enum/set, json and the other types read and written | `ColumnTypeReadTests`, `ColumnTypeEditTests` | ok (P11-25) |
| Grid editing incl. `AUTO_INCREMENT` read-back, trigger table, failing commit | `GridEditEndToEndTests` | ok (P11-33) |
| Designer (alter), DDL implicit-commit limit documented in the generator | `DesignerMatrixTests`, `MySqlAlterTests` | ok (P11-30) |
| ER diagram with the `er_stress` group | `ErStressTests` | ok (P11-31) |
| Export of every type | `ExportSnapshotScenario` | ok (P11-32) |

No new bug found by the MySQL run (the shared fixes of P11-36 and P11-37 apply to MySQL too).

## 2. Setup for the manual part

```bash
pwsh eng/run-docker-tests.ps1 -Engine mysql -Keep
```

It prints host, port, user `root`, password `squill-test` and database `squill`; leave that terminal open (Enter removes the container). Start the app with `$env:SQUILL_LANGUAGE='en'` and `dotnet run --project src/Squill.App.WinUI -c Release`.

## 3. Manual checklist

**Common features.** Repeat section 3 of [sqlite.md](sqlite.md) on this connection (tables are in the connection's database, no `sample.` prefix; the long statement is `select sleep(30)`; parameters are written `@name`, `?` shows the "positional not supported" message; the 100k query is `with recursive s(n) as (select 1 union all select n+1 from s where n<100000) select /*+ SET_VAR(cte_max_recursion_depth=200000) */ n from s`; the terminal check is the `mysql` client). Tick per area:
- [ ] Connections (dialog, validation fr/en, connect/reconnect, refresh)
- [ ] Query run (F5, Run All, parameters, cancel, error)
- [ ] Result grid (100k rows, copy, CSV, JSON)
- [ ] Grid editing (edit, add, delete, discard, commit, key, read-only, issues popup)
- [ ] Table designer (new, alter, preview, apply, failure)
- [ ] ER diagram (open, drag, save layout, SVG)
- [ ] Editor (completion, hover, diagnostics, format)
- [ ] History, snippets, settings
- [ ] Chrome (menu, rail, status bar, unsaved-tab prompt)
- [ ] Avalonia smoke (connect, run, grid, edit, designer, ER)

**MySQL specifics.**
- [ ] Wrong password: connect with a wrong password: an "access denied" message, no crash.
- [ ] TLS require: the container's server certificate is self-signed; "require" connects, "verify full" refuses with a readable message.
- [ ] Explorer: one level only (the database `squill` shown as the schema level) and it reads sensibly; the databases list lets you switch to `mysql`/another and the tree reloads.
- [ ] Routines and triggers nodes: `add_numbers`, `touch_customers`, `orders_stamp`; open each definition: a readable script without a `DELIMITER` line.
- [ ] `call touch_customers(1, 10)` runs; create `create procedure two_sets() begin select 1 as a; select 2 as b; end` and `call two_sets()`: two result tabs.
- [ ] Insert a row in `customers` through the grid: the generated `id` appears after commit (AUTO_INCREMENT read-back).
- [ ] `select * from all_types`: `tinyint(1)` shows true/false and can be edited as a boolean; `enum`/`set`, json and a zero date (`set session sql_mode=''; insert ... '0000-00-00'`) display without error.
- [ ] Completion: after ``select * from ` `` the tables are offered; a table with a space in its name is inserted with backticks; the generated DML of the grid uses backticks.
- [ ] Paging: `select * from orders limit 5 offset 5` runs; the grid shows 5 rows.
- [ ] `insert into customers (id, name) values (1, 'x') on duplicate key update name = 'y'` runs; the message says 2 rows affected on an existing id.
- [ ] Designer: apply an alteration; the preview or the result mentions that MySQL commits DDL implicitly (no rollback on failure).
- [ ] `select sleep(30)`: Esc after 2 s cancels; window stays responsive.
- [ ] Table-name case: create `create table MixedCase (id int primary key)`; refresh: the explorer shows it as MySQL stores it (`MixedCase` on Linux, lower case when `lower_case_table_names=1`).
- [ ] Terminal tab with the `mysql` client installed opens a session on this server (skip if not installed).

Result of the manual part (owner): ______ (date) - rows that failed: ______
