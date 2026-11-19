# P20-09 - Multi-table drop proved on the real engines

Status: todo
Depends on: P20-04

## Goal
The unit tests of P20-05 use a fake provider. Prove on SQLite (default run) and, Docker-gated, on PostgreSQL, MySQL and SQL Server that the statements of `TableDropPlan` + `DdlGenerators.DropTable` actually drop tables that reference each other, and that the atomicity promised in recipe 4 of [verification-recipe.md](verification-recipe.md) is what each engine does.

## Design decisions (final)
- Shared scenario `tests/Shared/DropTablesScenario.cs`, same pattern as `tests/Shared/MigrationRoundTripScenario.cs` and `DesignerScenarios.cs`: a static class taking an `IDatabaseConnection`, the dialect and a way to read the table names (read the existing scenarios and reuse their helpers), and the four tests per engine project reuse it:
  1. **Order**: create `p20_a`, `p20_b` (FK to `p20_a`), `p20_c` (FK to `p20_b`); `TableDropPlan.Build([a, b, c], schema)`; run the generated statements in `Order`; all three are gone.
  2. **Wrong order refuses** (documents why the order matters): dropping `p20_a` first fails on PostgreSQL, SQL Server and MySQL (SQLite with `PRAGMA foreign_keys = ON` too); the three tables still exist. Skip the assertion for SQLite when the connection does not enable foreign keys (read how the driver opens it and state it in the test).
  3. **Blocker**: `p20_d` references `p20_a`, only `p20_a` selected: `Blockers` has one entry and (not executed) the engine would have refused.
  4. **Atomicity**: PostgreSQL, SQL Server and SQLite: begin a transaction, run `DROP p20_x`, `DROP p20_y`, then a third statement that fails (`DROP TABLE p20_missing`), roll back: `p20_x` and `p20_y` still exist. MySQL: the same sequence leaves `p20_x` and `p20_y` dropped (implicit commit), which the test asserts so the "partial" message of P20-05 stays justified.
- Projects: `tests/Squill.Data.Sqlite.Tests` (always), `Squill.Data.Postgres.Tests`, `Squill.Data.MySql.Tests`, `Squill.Data.SqlServer.Tests` (`[Fact(SkipUnless = ..., SkipType = ...)]` with the existing Docker gate: copy it from a neighbor test file). Test names `Method_Scenario_Expected`. Table names are unique per run (prefix + short guid) and cleaned in `finally`.
- Run the Docker suites with `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres` (then `mysql`, `sqlserver`; Podman machine started; the MySQL `fs.aio-max-nr` note in `docs/verification/new-screens.md` applies). Write the counts in the Done notes.

## Out of scope
- The view-model flow (P20-05), Oracle (no scenario: record that it was not proved).

## Tasks
- [ ] Scenario and four test files; SQLite tests fail first only if `TableDropPlan` is missing, so write them after P20-04 and run them to see them pass; for the MySQL atomicity test state the expected outcome before running.
- [ ] Docker runs for the three engines, counts recorded; containers removed (`podman ps -a` empty).

## Acceptance criteria
- Default run green; Docker-gated run green per engine; `docs/testing.md` lists the new scenario if it lists the others (check the file).

## Risks
- SQL Server and MySQL need the sample schema loaded by the runner: use only the tables this scenario creates.
- A MySQL FK to a dropped parent fails with error 3730; that is the expected refusal of test 2, assert the failure, not the text.

## Suggested commit message
Prove the multi-table drop order and atomicity on SQLite, PostgreSQL, MySQL and SQL Server
