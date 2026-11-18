# P18-24 - Presentation: the import runner (risky)

Status: done
Depends on: P18-23

## Goal
Insert the converted rows into the target table in batches, with progress, cancel, a rejected-rows file and a transaction policy, on every engine. Recipe 1, 4.

## Design decisions (final)
- `src/Squill.Presentation/Import/ImportRunner.cs`: `RunAsync(ImportJob job, IProgress<ImportProgress> progress, CT)` returns `ImportResult(long Read, long Inserted, long Rejected, TimeSpan Elapsed, string? RejectsPath, ImportOutcome Completed | Cancelled | Failed | StoppedOnError)`. `ImportJob(session, TableInfo target, ImportMapping, ImportFormat, CsvOptions, string path, ImportOptions)`.
- Statements: the INSERT generator and parameter binding of the grid commit (P8-03, P8-09); read how `EditCommitter` binds parameters and reuse it. Batches of `BatchSize` rows (default 1,000, option) sent as one multi-row `INSERT` where the engine accepts the parameter count (SQL Server 2,100 parameters, SQLite 32,766, PostgreSQL 65,535, MySQL 65,535: the batch size is clamped to `limit / mappedColumns`), otherwise one statement per row inside the batch. Generated key columns (identity, serial) are never written unless mapped and the engine allows it (SQL Server `IDENTITY_INSERT` is **not** handled: mapping an identity column on SQL Server is refused by the validator with an issue; PostgreSQL `GENERATED ALWAYS` likewise).
- `ImportOptions`: `OnError` = `Reject` (default: skip the bad row, write it to the rejects file, go on) | `Stop` (first bad row stops, nothing already committed is undone unless `SingleTransaction`); `SingleTransaction` (all or nothing, default off for files over 100,000 rows with a hint) vs `PerBatch` (default, commit per batch); `RejectsPath` default `<file>.rejets.csv` next to the source (the `Écrire dans commandes_2026.rejets` choice of M10); `Truncate` is **not** offered.
- Transaction rules: inside an open user transaction of the session the runner uses savepoints per batch and never commits the user's transaction (phase 15 rules, read `SessionTransaction`); otherwise it opens its own short transactions. A database error on a batch under `Reject` retries that batch row by row to find the bad rows (bounded: a batch failing for a non-row reason, such as a lost connection or a missing table, stops with `Failed`).
- Production: starting an import on a Production connection asks the phase 15 confirmation (typed word: the table name) once before the first write.
- Progress: every 250 ms at most, `ImportProgress(long Read, long Inserted, long Rejected, double RowsPerSecond, TimeSpan? Remaining)` (`48 200 lignes/s . reste ~ 2 min` like M10); cancellation finishes the current batch, commits or rolls back according to the policy, and reports `Cancelled`.
- Memory bounded: one batch in memory; the reader streams (P18-22).
- The rejects file holds the original line plus an `erreur` column.

## Out of scope
- UI, upsert / conflict handling, creating the table, pause, several tables.

## Tasks
- [x] Runner, options, rejects writer, parameter clamping, savepoint integration, production confirmation call.
- [x] Tests with a fake session in `tests/Squill.Presentation.Tests/Import/`: batching and clamping, reject path, stop on error, cancel, progress throttling, savepoint use inside a user transaction, production refusal runs nothing.
- [x] Engine scenario in `tests/Shared` (SQLite always; PostgreSQL / MySQL / SQL Server Docker-gated): a 5,000-row CSV with 20 bad rows (bad number, too long text, NOT NULL violation), `Reject` gives 4,980 inserted and a rejects file with 20 lines; `Stop` stops at the first; `SingleTransaction` with a bad row leaves the table untouched; French decimals and dates land correctly; accents survive; 100,000 rows import in a bounded time (state the figure).

## Acceptance criteria
- Common acceptance (recipe 7), no head touched; Docker-gated runs reported per engine.

## Suggested commit message
Add the CSV import runner

## Result
SQLite engine scenario: 100,000 rows import in about 4.4 s (batches of 1,000, one transaction per batch). PostgreSQL / MySQL / SQL Server runs are Docker-gated (`SQUILL_DOCKER_TESTS=1`) and were not run here. Deviations: `SingleTransaction` stops at the first bad row (conversion or database) and does not bisect to name the database-rejected row in the rejects file; `ImportValidator.Validate` takes an optional dialect to refuse generated columns, and the runner refuses them too (`Failed`).
