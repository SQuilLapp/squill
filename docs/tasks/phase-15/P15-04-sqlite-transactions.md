# P15-04 - Data.Sqlite: transaction probe and the shared engine scenario

Status: todo
Depends on: P15-02, P15-03

## Goal
SQLite implements `ITransactionProbe`, and the engine-neutral scenario that the three server engines reuse is written and proven on SQLite (no container, runs everywhere). Recipe: section 4 (SQLite column), section 8.

## Design decisions (final)
- `SqliteDatabaseConnection` implements `ITransactionProbe` with `sqlite3_get_autocommit` (SQLitePCL raw API, on the handle of the `SqliteConnection`): autocommit mode = `InTransaction = false`, otherwise `true`; `Failed` is always false (SQLite has no aborted state; an error that rolled the transaction back shows as `InTransaction = false`). **First check** that the `Microsoft.Data.Sqlite` version of `Directory.Packages.props` exposes `SqliteConnection.Handle` and that `SQLitePCL.raw.sqlite3_get_autocommit` is callable and AOT-clean (the SQLite AOT probe publishes without IL2xxx/IL3xxx). If it is not, stop with a report and a proposed fix (the fallback would be `Unknown` always, which weakens P15-08 but keeps it correct).
- Nothing else changes in the provider: begin / commit / rollback go through `ExecuteAsync` with `TransactionSql`. `BEGIN` stays the deferred form.
- `tests/Shared/TransactionScenario.cs` (new, pattern of `DmlScenario.cs`): a static class taking `IDatabaseProvider`, a connection factory for **two connections to the same database** and an `EngineExpectations` record (`DdlTransactional`, `ErrorAbortsTransaction`, `OtherConnectionCanReadBeforeCommit`, `ProbeKnowsTransactions`, `ProbeKnowsFailure`, `SupportsActualPlan`). Scenarios, all through `IDatabaseConnection.ExecuteAsync` + `TransactionSql`: (1) commit makes the row visible on the second connection (and, when `OtherConnectionCanReadBeforeCommit`, it is not visible before); (2) rollback discards inserts, updates and deletes; (3) `CREATE TABLE` inside a transaction then rollback leaves no table when `DdlTransactional`, leaves it when not; (4) savepoint: insert, savepoint, insert, rollback to the savepoint, commit leaves exactly the first row (SQL Server uses `ROLLBACK TRANSACTION name`); (5) a unique-key violation inside a transaction: with `ErrorAbortsTransaction` the next statement fails, a rollback to a savepoint taken before the error recovers; without it the transaction stays usable and the earlier row commits; (6) the probe: `InTransaction` true after begin, false after commit / rollback when `ProbeKnowsTransactions`, otherwise `null`; `Failed` after the error when `ProbeKnowsFailure`; the probe on a connection with no transaction never throws; (7) disposing a connection with an open transaction discards its changes (checked from the second connection); (8) when `SupportsActualPlan`, an `IPlanReader` actual plan of a `SELECT` inside the open transaction does not end it (the earlier uncommitted insert is still there and still rolls back).
- `tests/Squill.Data.Sqlite.Tests/TransactionTests.cs` runs it with a temporary database file (two connections, file deleted afterwards) and `EngineExpectations(DdlTransactional: true, ErrorAbortsTransaction: false, OtherConnectionCanReadBeforeCommit: true, ProbeKnowsTransactions: true, ProbeKnowsFailure: false, SupportsActualPlan: false)`.

## Out of scope
- The three server engines (P15-05 ... P15-07), Presentation.

## Tasks
- [ ] Probe, scenario file, SQLite test class; AOT publish of `tools/Squill.AotProbe.Sqlite` (`rtk dotnet publish tools/Squill.AotProbe.Sqlite -c Release -r win-x64`).

## Acceptance criteria
- Common acceptance of the recipe, section 9, including the AOT publish of `tools/Squill.AotProbe.Sqlite`; all scenarios green with nothing skipped.

## Suggested commit message
Add the SQLite transaction probe and the shared transaction scenario
