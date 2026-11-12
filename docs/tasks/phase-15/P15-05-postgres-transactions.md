# P15-05 - Data.Postgres: transaction probe and engine proof

Status: todo
Depends on: P15-04, P11-02

## Goal
PostgreSQL implements `ITransactionProbe`; the shared scenario of P15-04 passes on a real server, with the failed-transaction behavior proven. Recipe: section 4 (PostgreSQL column), risks 4 and 6.

## Design decisions (final)
- `PostgresDatabaseConnection` implements `ITransactionProbe`: run `SELECT 1`; success -> `TransactionProbeResult.Unknown` (Npgsql exposes no public transaction status and the app tracks open/closed itself), `PostgresException` with SQLSTATE `25P02` (`in_failed_sql_transaction`) -> `Failed = true, InTransaction = true`; any other error -> `Unknown`. The probe must not alter the state of a healthy transaction (a `SELECT 1` never does).
- Expectations: `DdlTransactional: true, ErrorAbortsTransaction: true, OtherConnectionCanReadBeforeCommit: true, ProbeKnowsTransactions: false, ProbeKnowsFailure: true, SupportsActualPlan: true`.
- Extra PostgreSQL cases in `tests/Squill.Data.Postgres.Tests/TransactionTests.cs`: `CREATE INDEX CONCURRENTLY` inside an open transaction errors (the case `StatementTraits.CannotRunInTransaction` prevents, proves the list is right) and leaves the transaction `Failed`; `VACUUM` the same; after a failed statement `ROLLBACK TO SAVEPOINT` returns the probe to not-failed and the transaction commits the earlier rows; `ROLLBACK` clears it; the plan reader `Analyser` inside a `Failed` transaction throws a server error (the app refuses before, P15-09, but the provider must not hang or leave `BEGIN READ ONLY` pending); `BEGIN READ ONLY` warning `25001` path (already in P13-06) still passes with an open user transaction.
- Same container image and runner as phase 11 (`postgres:17-alpine`, `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres`).

## Out of scope
- Presentation, other engines.

## Tasks
- [ ] Probe, test class using `TransactionScenario`, the extra cases, AOT publish of `tools/Squill.AotProbe.Postgres`, Docker run.

## Acceptance criteria
- Common acceptance of the recipe, section 9 (Docker run green, nothing skipped, no container left).

## Suggested commit message
Add the PostgreSQL transaction probe and prove transactions on the server
