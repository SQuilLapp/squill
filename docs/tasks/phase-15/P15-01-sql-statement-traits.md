# P15-01 - Sql: statement traits (what a statement does to data, schema and the transaction)

Status: done
Depends on: none

## Goal
One pure classifier answers, for a single statement and a dialect, everything phase 15 needs: read-only or not, modifies data, changes the schema, destructive, missing `WHERE`, the transaction control it contains, whether it commits implicitly, whether it cannot run in a transaction. Read [transactions-recipe.md](transactions-recipe.md) sections 2 (decisions 2, 7, 9, 11), 4 and 6 first.

## Design decisions (final)
- New folder `src/Squill.Sql/Transactions/`: `public enum StatementKind { Empty, Select, Insert, Update, Delete, Merge, Truncate, Create, Alter, Drop, Transaction, Call, Set, Other }` (`Call` = `CALL`, `EXEC`, `EXECUTE`, `DO`; `Set` = `SET`, `PRAGMA`, `USE`, `SHOW`-like session statements; `Other` = anything unrecognised), `public enum TransactionControl { None, Begin, Commit, Rollback, Savepoint, RollbackToSavepoint, ReleaseSavepoint }`, `public sealed record StatementTraits(StatementKind Kind, bool ReadOnly, bool ModifiesData, bool ChangesSchema, bool Destructive, bool NoWhere, TransactionControl Control, bool ImplicitCommit, bool CannotRunInTransaction, bool MayChangeTransaction, string? Target)` and `public static class StatementClassifier { public static StatementTraits Classify(string statement, DatabaseDialect dialect); }`.
- Token based like `PlanEligibility` (`SqlTokenizers.For(dialect)`, comments and whitespace skipped, only the first statement, leading `(` skipped), **no parser**. Move the private `WritesData` scan of `PlanEligibility` into an `internal` helper shared by both (`PlanEligibility` behavior and its tests unchanged); `WITH ... SELECT` is read-only unless the scan finds `INTO` / `DELETE` / `UPDATE` / `INSERT` / `MERGE`; `WITH ... DELETE/UPDATE/INSERT` is that data-modifying kind.
- `ReadOnly` is true only for `Select` (not `SELECT ... INTO`; `SELECT ... FOR UPDATE` stays read-only), `Set`, `Transaction` of kind `Rollback` (nothing to undo is not a write) and `Empty`; `Call` and `Other` are **not** read-only (decision 2). `Set` and `Empty` do not open a transaction in manual mode.
- `ModifiesData`: `Insert`, `Update`, `Delete`, `Merge`, `Truncate`, `SELECT ... INTO`, MySQL `REPLACE`, SQLite `REPLACE` / `INSERT OR ...`. `ChangesSchema`: `Create`, `Alter`, `Drop`, `Truncate` (also data), `RENAME`, `COMMENT ON`, `GRANT`, `REVOKE`. `Call` and `Other`: both flags false but `ReadOnly` false (the guard of P15-12 treats them as "may modify").
- `NoWhere`: `UPDATE` or `DELETE` with no top-level `WHERE` (parenthesis depth 0, after the head; `DELETE ... WHERE CURRENT OF` counts as a `WHERE`). `Destructive` = `NoWhere` or `Truncate` or `Drop` or `Alter` containing a top-level `DROP`. `Target` = the table name as written for `Insert` / `Update` / `Delete` / `Truncate` / `Drop table` (schema-qualified text kept, quotes removed), else null; used for the dialog title only.
- `Control`: `BEGIN [TRANSACTION|WORK]`, `START TRANSACTION`, `BEGIN TRAN[SACTION]` -> `Begin`; `COMMIT`, `END`, `COMMIT TRAN[SACTION]` -> `Commit`; `ROLLBACK` / `ROLLBACK TRAN[SACTION] [name]` / `ABORT` -> `Rollback`, but `ROLLBACK TO [SAVEPOINT] n` and (SQL Server) `ROLLBACK TRANSACTION name` -> `RollbackToSavepoint`; `SAVEPOINT n` and (SQL Server) `SAVE TRAN[SACTION] n` -> `Savepoint`; `RELEASE [SAVEPOINT] n` -> `ReleaseSavepoint`. MySQL `BEGIN` is a transaction begin (a statement block `BEGIN ... END` never starts a statement). `Kind` is `Transaction` for all of them.
- `ImplicitCommit` (MySQL only, false elsewhere): implementer reads the MySQL 8.4 manual page "Statements That Cause an Implicit Commit" and lists in a comment-free `static readonly` set the heads they include: at least `CREATE` / `ALTER` / `DROP` / `RENAME` / `TRUNCATE` of any object except `CREATE|DROP TEMPORARY TABLE`, `LOCK TABLES`, `UNLOCK TABLES`, `START TRANSACTION` / `BEGIN` (they commit an open one), `SET autocommit = 1`, `ANALYZE|OPTIMIZE|REPAIR|CHECK TABLE`, `FLUSH`, `GRANT`, `REVOKE`, `CREATE|DROP USER`, `LOAD DATA` is **not** one. The test lists every included head.
- `CannotRunInTransaction`: PostgreSQL `VACUUM`, `CREATE|DROP DATABASE`, `CREATE|DROP TABLESPACE`, `CREATE|DROP INDEX ... CONCURRENTLY`, `REINDEX ... CONCURRENTLY`, `ALTER SYSTEM`; SQL Server `CREATE|ALTER|DROP DATABASE`, `BACKUP`, `RESTORE`; SQLite `VACUUM`, `ATTACH`, `DETACH`; MySQL none.
- `MayChangeTransaction`: `Call`, `SET` of `autocommit` / `IMPLICIT_TRANSACTIONS` / `XACT_ABORT`, `PRAGMA`, `LOCK TABLES`, `Other`.
- Oracle is classified through the generic rules, not verified.

## Out of scope
- Using the classifier anywhere (P15-08 ...), parsing, multi-statement input (callers pass one statement from `RunTarget`).

## Tasks
- [x] `Squill.Sql/Transactions` types and classifier, shared scan helper extracted from `PlanEligibility`.
- [x] `tests/Squill.Sql.Tests/Transactions/StatementClassifierTests.cs`: table-driven per dialect (about 120 rows): every kind, `WITH` forms, `SELECT ... INTO`, `FOR UPDATE`, comments before the head, `;` and several statements (first only), `UPDATE` / `DELETE` with and without `WHERE` (also `WHERE` only inside a subquery = no top-level `WHERE`), `ALTER TABLE ... DROP COLUMN`, all `Control` forms per dialect, MySQL implicit-commit list (positive and `CREATE TEMPORARY TABLE` negative), the not-allowed-in-transaction lists, `Call` / `Other` not read-only. Existing `PlanEligibility` tests stay green.

## Acceptance criteria
- Common acceptance of the recipe, section 9.

## Suggested commit message
Classify statements: read-only, data, schema, destructive, transaction control
