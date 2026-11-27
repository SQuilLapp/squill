# P21-02 - MariaDB: execution plan, transaction probe and the support matrix

Status: todo
Depends on: P21-01

## Goal
`Expliquer` / `Analyser` and manual transactions on MariaDB, then MariaDB as a column of the support matrix.

Exists today: the MySQL plan reader parses `EXPLAIN FORMAT=JSON` (MySQL 8 shape) and `EXPLAIN ANALYZE` text (P13-07, P13-08); README says "MariaDB and Oracle are not supported for plans". The MySQL transaction probe always answers unknown (P15-06).

## Design decisions (final)
- MariaDB `EXPLAIN FORMAT=JSON` has a different shape (`query_block` with `table` / `nested_loop`, no `cost_info`): a new pure `MariaDbPlanParser` in `Squill.Data.MySql` next to `MySqlPlanParser`, chosen when the profile has `MariaDb`. `Analyser` uses `ANALYZE FORMAT=JSON <statement>` (actual rows `r_rows`, time `r_total_time_ms`), read-only statements only, same eligibility as MySQL (`PlanEligibility`).
- A figure MariaDB does not give is omitted, never invented (phase 13 rule).
- Transactions: same as MySQL (DDL commits implicitly, `ImplicitCommit` question); the probe stays "unknown" unless `@@in_transaction` exists (it does in MariaDB 10.x+): use it for MariaDB only.
- ADR 0008: add a MariaDB column to the support matrix from the P21-01 results; README "Supported databases" table gets a MariaDB row; remove "MariaDB (not tested, not a goal)" from the not-supported lists (ADR 0008, README).

## Out of scope
- Plan comparison or export (not built for any engine).

## Tasks
- [ ] Golden-file tests of `MariaDbPlanParser` (estimated and analyzed JSON captured from the container).
- [ ] Docker-gated tests: one plan per mode through the shared engine scenario (P13-11), the probe in and out of a transaction.
- [ ] Docs: ADR 0008 matrix column, README rows, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine mariadb` green; README and ADR 0008 updated.

## Risks
- MariaDB JSON plans vary between 10.6 and 11.x: the parser ignores unknown keys and the tests use 11.4.

## Open points (defaults)
- None.

## Suggested commit message
Read MariaDB execution plans and transaction state, and add MariaDB to the support matrix
