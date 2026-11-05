# Verification report: PostgreSQL

Card: [P11-37](../tasks/phase-11/P11-37-verify-postgres.md). Status: **automated part done, manual part pending the owner** (no automated session can drive the WinUI window).

## 1. Automated (done)

Run with `pwsh eng/run-docker-tests.ps1 -Engine postgres` (81 + 1 tests, all green, container removed afterwards).

| Area | Test | Result |
|---|---|---|
| Query tab flow on a live server: several statements, run under the caret, failure stops the script, `@name` parameter prompt and bound value, `$1` refused, `pg_sleep(30)` cancelled, 100,000 rows from `generate_series`, CSV and JSON export, completion on `public.` | `QueryFlowTests` | ok |
| `INSERT ... RETURNING` gives a result set | `PostgresVerificationTests` | ok |
| Wrong password: readable message | `PostgresVerificationTests` | ok |
| Quoted mixed-case identifiers in completion (`public.`, `"Mix`) | `PostgresVerificationTests`, `SchemaCompletionTests` | ok, fixed in this card |
| `DO $$ ... raise notice` messages | `MultiResultTests` | ok (P11-11) |
| TLS require / verify-full, command timeout, second database listing | `PostgresDatabaseProviderTests` | ok |
| Schemas, identity/serial, FKs with every action, views, materialized views, routines, triggers and their definitions | `PostgresSchemaReaderTests` | ok |
| Arrays, json, uuid, interval, bytea and the other types read and written | `ColumnTypeReadTests`, `ColumnTypeEditTests` | ok (P11-25) |
| Grid editing incl. uuid and typed keys, trigger table, failing commit | `GridEditEndToEndTests` | ok (P11-33) |
| Designer incl. `serial`, identity, `USING` type change | `DesignerMatrixTests`, `PostgresAlterTests` | ok (P11-30) |
| ER diagram with the `er_stress` group | `ErStressTests` | ok (P11-31) |
| Export of every type | `ExportSnapshotScenario` | ok (P11-32) |
| SSH tunnel to PostgreSQL behind a jump host | `-Engine ssh` | ok (P11-35) |

Bugs found by the new scenario and fixed (regression tests listed above):

1. PostgreSQL named parameters did nothing: `@name` was not recognised by the PostgreSQL tokenizer, so the prompt never appeared and the server got a literal `@name`. The tokenizer now recognises `@name` (not the operators `<@`, `@>`, `@-5`); `ParameterScannerTests`.
2. Completion inserted `MixedCase` unquoted, which PostgreSQL folds to `mixedcase` (relation does not exist). It now inserts `"MixedCase"`; and a prefix typed after an opening quote (`"Mix`) now completes (it offered keywords only). `SchemaCompletionTests`.

Shared with every engine (fixed in P11-36): rows-affected message for DML.

## 2. Setup for the manual part

```bash
pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep
```

It prints host, port, user, password (`squill-test`) and database; leave that terminal open (Enter removes the container). The sample schema is loaded in schema `sample`. Start the app with `$env:SQUILL_LANGUAGE='en'` and `dotnet run --project src/Squill.App.WinUI -c Release`.

## 3. Manual checklist

**Common features.** Repeat section 3 of [sqlite.md](sqlite.md) on this connection (use `sample.customers` instead of `customers`; the long statement is `select pg_sleep(30)`; parameters are written `@name`, `$1` shows the "positional not supported" message; the 100k query is `select g from generate_series(1,100000) g`; the terminal check is `psql`). Tick per area:
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

**PostgreSQL specifics.**
- [ ] Wrong password: edit the connection, type a wrong password, connect: the message mentions the password, no crash.
- [ ] TLS: set TLS to "require" (the test container has TLS on with a self-signed certificate only in the automated fixture; with `-Keep` the server has none, so expect a clear refusal message) - note what you see.
- [ ] Explorer: schemas `public` and `sample`; under `sample`: tables, views, the materialized view `customer_counts`, functions/procedures, triggers. Open the definition of the view, the materialized view, a function and a trigger (context menu): the script is readable.
- [ ] Database switch: use the database control of the status bar / tree to switch to another database; the tree reloads.
- [ ] `insert into sample.no_pk (label, amount) values ('x', 1) returning *;` shows the returned row in a grid.
- [ ] `do $$ begin raise notice 'hi'; end $$;` shows a Messages tab with `hi`.
- [ ] `select * from sample.all_types`: arrays, json, uuid, interval, bytea cells display sensibly; edit an array cell and a json cell, commit, re-run.
- [ ] Edit a row of a table with a `uuid` primary key (create one: `create table sample.u (id uuid primary key default gen_random_uuid(), n text)`), change `n`, commit.
- [ ] `select pg_sleep(30)`: Esc after 2 s cancels, window stays responsive.
- [ ] Designer on a `serial` and an identity column (`sample.orders`, `sample.customers`): change a column type from `integer` to `text` shows a `USING` clause in the preview; apply.
- [ ] ER diagram: the whole `sample` schema including the FK-action tables is readable.
- [ ] Completion: after `select * from "` the mixed-case table (create `public."MixedCase"`) is offered and inserted with its quotes; `public.` lists the tables.
- [ ] Terminal tab with `psql` installed opens a `psql` session on this server (skip if `psql` is not installed).
- [ ] SSH tunnel (only if you have a jump host; the automated `-Engine ssh` run already proved it).

Result of the manual part (owner): ______ (date) - rows that failed: ______
