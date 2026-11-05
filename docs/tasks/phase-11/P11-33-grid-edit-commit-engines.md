# P11-33 - Grid edit and commit through the Presentation layer on every engine

Status: done
Depends on: P11-02, P11-03, P11-04, P11-16, P11-26

## Goal
`EditableSourceAnalyzer`, `EditableGridDataSource`, `ChangeSetBuilder` and `EditCommitter` work on real connections for each dialect; P8-13 tested the DML generator on drivers, and P8-09 the committer only with fakes.

## Design decisions (final)
- One class `GridEditEndToEndTests` per driver test project (SQLite never skipped, others Docker-gated; add a project reference to `Squill.Presentation` where missing, check the dependency graph still holds: tests may reference anything). Scenario run through the real `ResultEditorViewModel`: run `select * from items` through `SqlRunner`/`QueryTabViewModel` with a real session, edit cells, add and delete rows, commit, re-query, compare.
- Cases (per dialect, schema-qualified source: `public.items`, `[dbo].[items]`, `` `db`.`items` ``, `main.items`, and an alias `select i.* from items i`): update by single key, update a key column, composite key, insert with generated key and the grid showing the new key, insert on a table whose only columns have defaults, delete several rows in one commit, NULL in a non-key column, a table **without primary key** (read-only with reason), a view (read-only), a join (read-only), a failing row in the middle (unique violation): the whole commit rolls back, the grid keeps the pending changes and marks the failing row with the driver message, a row deleted by someone else between load and commit (update affects 0 rows: the committer must report a concurrency issue, not succeed silently; implement if absent), a table with a trigger (SQL Server `OUTPUT INSERTED` fails on tables with triggers: use `OUTPUT ... INTO @t` or `SCOPE_IDENTITY()`; PostgreSQL `RETURNING` unaffected; fix `DmlGenerator` read-back and test in `Squill.Sql.Tests`), key values of the types of P11-26 (uuid key, string key with trailing spaces on SQL Server `char` padding, decimal key).
- Results of P11-04's trigger finding are fixed here.

## Out of scope
- Heads (P11-36 ... P11-39), new edit features.

## Tasks
- [x] Test classes, concurrency detection, read-back fix, any bug found.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.

## Suggested commit message
Test grid editing end to end on every engine and fix what it found
