# Verification report: SQL Server

Card: [P11-39](../tasks/phase-11/P11-39-verify-sqlserver.md). Status: **automated part done, manual part pending the owner** (no automated session can drive the WinUI window).

## 1. Automated (done)

Run with `pwsh eng/run-docker-tests.ps1 -Engine sqlserver` (102 tests; the 100 existing ones and the six new ones are green, container removed afterwards).

| Area | Test | Result |
|---|---|---|
| Query tab flow on a live server: several statements, run under the caret, failure stops the script, `@name` parameter prompt and bound value, `waitfor delay` cancelled, 100,000 rows, CSV and JSON export, completion on `dbo.` | `QueryFlowTests` | ok |
| Wrong password: "Login failed" message | `SqlServerVerificationTests` | ok |
| Untrusted certificate with trust off (verify full): the message names the certificate (in the OS language) | `SqlServerVerificationTests` | ok |
| A script with `GO` lines: Run All sends three batches, `nvarchar` Unicode (`日本語 ✓ é`) comes back intact | `SqlServerVerificationTests` | ok |
| `OFFSET ... FETCH` paging | `SqlServerVerificationTests` | ok |
| `[bracket]` quoting in completion, and an opening `[` is replaced | `SqlServerVerificationTests`, `SchemaCompletionTests` | ok |
| `EXEC` of a procedure with two result sets and `PRINT` (Messages) | `MultiResultTests` | ok (P11-11) |
| Trust flag on loopback, command timeout, second database listing | `SqlServerDatabaseProviderTests` | ok |
| Schemas (`dbo`, `sample`), routines, triggers, view definitions | `SqlServerSchemaReaderTests` | ok |
| `uniqueidentifier`, `datetimeoffset`, `money`, `xml`, `rowversion` and the other types | `ColumnTypeReadTests`, `ColumnTypeEditTests` | ok (P11-25) |
| Grid editing incl. `OUTPUT`/identity read-back with a trigger table, failing commit | `GridEditEndToEndTests` | ok (P11-33) |
| Designer: default constraint drop, `sp_rename`, identity add refusal | `DesignerMatrixTests`, `SqlServerAlterTests` | ok (P11-30) |
| ER diagram with the `er_stress` group | `ErStressTests` | ok (P11-31) |
| Export of every type | `ExportSnapshotScenario` | ok (P11-32) |

No new bug found by the SQL Server run beyond the shared fixes of P11-36/P11-37 (rows-affected text, bracket-prefix completion).

## 2. Setup for the manual part

```bash
pwsh eng/run-docker-tests.ps1 -Engine sqlserver -Keep
```

It prints host, port, user `sa`, password `Squill-test1` and database `master`; the sample schema is in schema `sample`. Leave that terminal open (Enter removes the container). Start the app with `$env:SQUILL_LANGUAGE='en'` and `dotnet run --project src/Squill.App.WinUI -c Release`. Connect with host `127.0.0.1` (the certificate is trusted for loopback only).

## 3. Manual checklist

**Common features.** Repeat section 3 of [sqlite.md](sqlite.md) on this connection (use `sample.customers`; the long statement is `waitfor delay '00:00:30'`; parameters are written `@name`, there is no positional form; the 100k query is `select top (100000) row_number() over (order by (select null)) n from sys.all_objects a cross join sys.all_objects b`; the terminal check is `sqlcmd`). Tick per area:
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

**SQL Server specifics.**
- [ ] Login `sa`: connects. Wrong password: "Login failed for user 'sa'", no crash.
- [ ] Certificate: host `127.0.0.1` with "trust server certificate" off and TLS "verify full": the error is readable and names the certificate; switch trust on: it connects.
- [ ] Explorer: schemas `dbo` and `sample`; tables, views, procedures/functions, triggers under `sample`; open the definition of a view, a procedure and a trigger.
- [ ] Database switch without `USE`: pick another database in the database control; the tree reloads and a query runs there.
- [ ] A script `select 1 as a` / `GO` / `select 2 as b` with Run All: two results; `GO` is not sent to the server.
- [ ] `exec sample.touch_customers 1, 10` (or any procedure with two `select`s plus a `print 'hi'`): result tabs per set and a Messages tab with `hi`.
- [ ] Insert a row in `sample.customers` through the grid: the identity value appears after commit; on a table with a trigger (`sample.orders`) the commit still reads the key back (`OUTPUT`).
- [ ] `select * from sample.all_types`: `uniqueidentifier`, `datetimeoffset`, `money`, `xml` display and edit; the `rowversion` cell is read-only; `nvarchar` with Unicode text (`N'日本語 ✓'`) round-trips through an edit.
- [ ] Paging: `select * from sample.orders order by id offset 5 rows fetch next 5 rows only` shows 5 rows.
- [ ] `waitfor delay '00:00:30'`: Esc after 2 s cancels; window stays responsive.
- [ ] Designer: drop a column that has a default constraint (the preview drops the constraint first); rename a column (`sp_rename` in the preview); adding an identity to an existing column is refused with a message.
- [ ] ER diagram of `sample` including the `er_stress` group is readable.
- [ ] Completion: `select * from sample.[` offers bracketed names; accepting `[odd table]` (create `create table sample.[odd table] (id int primary key)`) keeps the brackets and does not double the `[`.
- [ ] Terminal tab with `sqlcmd` installed opens a session on this server (skip if not installed).

Result of the manual part (owner): ______ (date) - rows that failed: ______
