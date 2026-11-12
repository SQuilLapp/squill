# P15-02 - Sql: transaction statement text and savepoints per dialect

Status: done
Depends on: none

## Goal
One place builds the begin / commit / rollback / savepoint statements of each dialect, so `SqlRunner`, `SessionTransaction` and the tests agree. Today `SqlRunner` (`src/Squill.Presentation/Sessions/SqlRunner.cs`) holds ternaries (`BEGIN` vs `BEGIN TRANSACTION`). Recipe: section 4 (engine matrix).

## Design decisions (final)
- `public static class TransactionSql` in `src/Squill.Sql/Transactions/`: `Begin(DatabaseDialect)`, `Commit(dialect)`, `Rollback(dialect)` (SQLite / PostgreSQL `BEGIN` / `COMMIT` / `ROLLBACK`, MySQL `START TRANSACTION` / `COMMIT` / `ROLLBACK`, SQL Server `BEGIN TRANSACTION` / `COMMIT TRANSACTION` / `ROLLBACK TRANSACTION`, Oracle `COMMIT` / `ROLLBACK`, begin is implicit: returns `null`), `Savepoint(dialect, name)`, `RollbackToSavepoint(dialect, name)`, `ReleaseSavepoint(dialect, name)` (SQL Server: `SAVE TRANSACTION n`, `ROLLBACK TRANSACTION n`, release returns `null`; others `SAVEPOINT n` / `ROLLBACK TO SAVEPOINT n` / `RELEASE SAVEPOINT n`), `SupportsSavepoints(dialect)`.
- `name` must match `^[A-Za-z_][A-Za-z0-9_]{0,40}$` (`ArgumentException` otherwise, never quoted: the app only generates `squill_sp_<n>`); `SavepointName(int n)` returns that form.
- Pure, no connection, AOT-safe (plain string switch).

## Out of scope
- Executing any of it (P15-08, P15-09); changing `SqlRunner` (P15-09).

## Tasks
- [x] `TransactionSql` and tests `tests/Squill.Sql.Tests/Transactions/TransactionSqlTests.cs`: exact text per dialect for all six methods, `null` returns, invalid names rejected, `squill_sp_3`.

## Acceptance criteria
- Common acceptance of the recipe, section 9.

## Suggested commit message
Add per-dialect transaction and savepoint statement text
