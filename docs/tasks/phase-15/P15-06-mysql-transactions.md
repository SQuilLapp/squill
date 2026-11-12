# P15-06 - Data.MySql: transaction probe, implicit commit proof

Status: todo
Depends on: P15-04, P11-03

## Goal
MySQL implements `ITransactionProbe`; the shared scenario passes on MySQL 8.4 and the implicit-commit behavior the whole phase relies on is **proven**, not assumed. Recipe: section 4 (MySQL column), risk 1.

## Design decisions (final)
- Probe: the server has no documented "am I in a transaction" variable (do not use `@@in_transaction`, it is MariaDB). **Try, in this order, on the 8.4 container with the unprivileged test user**, and keep the first that works without extra grants: (a) `SELECT COUNT(*) FROM performance_schema.events_transactions_current WHERE thread_id = PS_CURRENT_THREAD_ID() AND state = 'ACTIVE'`, (b) `SELECT COUNT(*) FROM information_schema.innodb_trx WHERE trx_mysql_thread_id = CONNECTION_ID()`. Result: `InTransaction = count > 0`, `Failed = false`; any error (missing table, privilege, performance schema disabled) -> `Unknown`. **Stop rule**: if neither is reliable (false negatives when the transaction has only read, or needs a privilege the app's users usually lack), implement the probe as always `Unknown`, set `ProbeKnowsTransactions: false`, and write that in the card report and in the ADR; P15-08 stays correct without it. A transaction that has only run `SELECT`s may not appear in `innodb_trx`: record what the tests observe.
- Expectations: `DdlTransactional: false, ErrorAbortsTransaction: false, OtherConnectionCanReadBeforeCommit: true` (default REPEATABLE READ: the other connection sees the old data, not blocked), `ProbeKnowsTransactions` as found, `ProbeKnowsFailure: false`, `SupportsActualPlan: true`.
- Extra cases in `tests/Squill.Data.MySql.Tests/TransactionTests.cs`: `START TRANSACTION; INSERT; CREATE TABLE x (...); ROLLBACK` leaves the insert **committed** (proves the implicit commit and the `StatementClassifier` `ImplicitCommit` rule for `CREATE`); the same for `ALTER TABLE`, `TRUNCATE TABLE`, `DROP TABLE`, `LOCK TABLES`, `START TRANSACTION` issued while one is open, and **not** for `CREATE TEMPORARY TABLE` (one assertion per head of the list in `P15-01`; the classifier test and this one must name the same heads); savepoints are gone after an implicit commit (`ROLLBACK TO SAVEPOINT` errors); a deadlock between two connections rolls the whole transaction back (assert the probe or the data, and which error the driver gives; two connections, a fixed lock order, a 5 s ceiling); `BEGIN` inside an open transaction commits it (the reason `SqlRunner` must not be called inside one).
- Image and runner: `mysql:8.4`, `rtk pwsh eng/run-docker-tests.ps1 -Engine mysql`.

## Out of scope
- Presentation, MyISAM / non-transactional engines (documented only).

## Tasks
- [ ] Probe with the stop rule, test class using `TransactionScenario`, the implicit-commit cases, AOT publish of `tools/Squill.AotProbe.MySql`, Docker run.

## Acceptance criteria
- Common acceptance of the recipe, section 9. The report states which probe query was kept and what the tests showed.

## Suggested commit message
Add the MySQL transaction probe and prove implicit commits
