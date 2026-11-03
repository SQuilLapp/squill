# P11-03 - MySQL container tests run and pass

Status: todo
Depends on: P11-01

## Goal
Every Docker-gated test of `tests/Squill.Data.MySql.Tests` has actually run against MySQL 8.4 and passes.

## Design decisions (final)
- Same method and rules as P11-02 (classification of failures, no weakening, assembly-level fixture if slow), with `-Engine mysql`.
- MySQL specifics to check while fixing: upper-case `information_schema` result columns under MySQL 8, `database()` being the only schema, generated-key read-back through `LAST_INSERT_ID()`, `utf8mb4` text, `tinyint(1)` as boolean, DDL causing implicit commits (the designer runs DDL outside a transaction).
- `tests/Shared/SampleSchema/mysql.sql` with the same objects as the Postgres one (MySQL types: tinyint, smallint, int, bigint, decimal(18,4), float, double, bit(1), char, varchar, text, blob, date, time, datetime(3), timestamp, year, json, enum, set, an AUTO_INCREMENT column) and the same test `SampleSchema_LoadsAndIsReadBack`.

## Out of scope
- Display and edit behaviour of those types (P11-25, P11-26), MariaDB.

## Tasks
- [ ] Run, fix, add sample schema and test, run twice green.

## Acceptance criteria
- `rtk pwsh eng/run-docker-tests.ps1 -Engine mysql` is green; the report gives tests run and failures fixed.
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- No container left.

## Suggested commit message
Run and fix the MySQL container tests, add the sample schema
