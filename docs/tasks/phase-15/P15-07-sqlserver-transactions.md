# P15-07 - Data.SqlServer: transaction probe and engine proof

Status: done
Depends on: P15-04, P11-04

## Goal
SQL Server implements `ITransactionProbe`; the shared scenario passes on SQL Server 2022; nested-transaction and doomed-transaction behavior is proven. Recipe: section 4 (SQL Server column), risk 5.

## Design decisions (final)
- Probe: `SELECT @@TRANCOUNT, XACT_STATE()` through the connection: `InTransaction = trancount > 0`, `Failed = xact_state = -1` (doomed: only rollback is possible); any error -> `Unknown`. `XACT_STATE()` works inside a doomed transaction, plain `SELECT` of other tables does not.
- Expectations: `DdlTransactional: true, ErrorAbortsTransaction: false, OtherConnectionCanReadBeforeCommit: false` (default READ COMMITTED blocks a reader on an uncommitted row: that check is skipped, the other checks use a second connection only after commit), `ProbeKnowsTransactions: true, ProbeKnowsFailure: true, SupportsActualPlan: true`.
- Extra cases in `tests/Squill.Data.SqlServer.Tests/TransactionTests.cs`: `BEGIN TRANSACTION` twice gives `@@TRANCOUNT` 2 and one `COMMIT` leaves it 1 (why `SqlRunner.BeginAsync` inside a user transaction is wrong); unnamed `ROLLBACK TRANSACTION` with two levels open resets it to 0 (why a savepoint scope uses `ROLLBACK TRANSACTION name`); `SAVE TRANSACTION a` then `ROLLBACK TRANSACTION a` keeps the transaction and the earlier rows; `SET XACT_ABORT ON` then a failing statement dooms the transaction (`Failed` true) and only `ROLLBACK` clears it; `CREATE DATABASE` inside a transaction errors (proves the `CannotRunInTransaction` list); the plan reader `Analyser` (STATISTICS XML, savepoint) inside the open user transaction leaves it open and its earlier uncommitted insert intact (P13-10 behavior, re-proven here).
- Image and runner: `mcr.microsoft.com/mssql/server:2022-latest`, `rtk pwsh eng/run-docker-tests.ps1 -Engine sqlserver`.

## Out of scope
- Presentation, `SET IMPLICIT_TRANSACTIONS` modeling.

## Tasks
- [x] Probe, test class using `TransactionScenario`, the extra cases, AOT publish of `tools/Squill.AotProbe.SqlServer`, Docker run.

## Acceptance criteria
- Common acceptance of the recipe, section 9.

## Suggested commit message
Add the SQL Server transaction probe and prove nested and doomed transactions

## Result (recorded by the implementing session)
- A doomed transaction (`XACT_STATE() = -1`) cannot outlive its batch: with `XACT_ABORT ON` a failing statement inside `TRY/CATCH` dooms it, and SQL Server rolls it back at the end of the batch (error 3998); a plain error under `XACT_ABORT ON` ends the transaction (`@@TRANCOUNT` 0). So the probe's `Failed` is never seen between two statements on 2022; the probe still maps `-1` (multi-statement batch). Both cases are tests.
- The shared `TransactionScenario` step 5 now asserts `Failed == ErrorAbortsTransaction` when `ProbeKnowsFailure` (a duplicate key error does not doom a SQL Server transaction), which the card's expectations (`ErrorAbortsTransaction: false`, `ProbeKnowsFailure: true`) required.
- `SqlServerDatabaseConnection.DisposeAsync` now sends `IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION` (the pool only resets on reuse; locks stayed until then).
- AOT probe: only the known third-party `IL2104` / `IL3053` of Microsoft.Data.SqlClient (see `docs/poc/aot-drivers.md`), none in Squill code.
