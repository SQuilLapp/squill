# P21-97 - MariaDB: blocked statement text in the activity reader

Status: todo
Depends on: P21-01

## Goal
`ActivityReaderTests.ReadAsync_BlockedUpdate_ShowsStatesEdgeAndLimits` fails on `mariadb:11.4`: the blocked session shows a null statement text (expected to contain `activity_t`). Find what `MySqlActivityReader` reads from `information_schema.PROCESSLIST` / `performance_schema` that MariaDB fills differently (the statement text, the blocking edge) and handle it in the reader.

## Design decisions (final)
- Fix inside `Squill.Data.MySql` only; the test stays shared and runs on both images (`rtk pwsh eng/run-docker-tests.ps1 -Engine mariadb`).

## Out of scope
- Plans (P21-02).

## Tasks
- [ ] Reproduce with the MariaDB container, write the failing assertion, fix the reader.

## Acceptance criteria
- Recipe 2 common acceptance; the test passes on `mysql:8.4` and `mariadb:11.4`.

## Suggested commit message
Read the blocked statement text of a MariaDB session in the activity view
