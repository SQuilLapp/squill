# P11-02 - PostgreSQL container tests run and pass

Status: done
Depends on: P11-01

## Goal
Every Docker-gated test of `tests/Squill.Data.Postgres.Tests` (provider, schema reader, ALTER, DML round trip) has actually run against PostgreSQL 17 and passes; none was ever executed before.

## Design decisions (final)
- Run `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres`. Each failure is classified: test bug (fix the test, keep its intent), provider or reader bug (fix the source with a regression test), or environment (document in `docs/testing.md`). A test is never deleted, loosened or skipped to get green; an assertion changes only when the expectation was wrong, and the report lists each such change with the reason.
- Fixture: one container per test class today; switch to one container for the whole test project (xunit.v3 assembly or collection fixture) if startup time exceeds ~60 s in total.
- Create `tests/Shared/SampleSchema/postgres.sql` as described in `verification-recipe.md` section 3 (idempotent, safe to run twice) and a test `SampleSchema_LoadsAndIsReadBack` that runs it and checks that the schema reader returns every object (tables, view, index, each FK action, the table without PK, the all-types table).
- The all-types table has at least: smallint, integer, bigint, numeric(18,4), real, double precision, boolean, text, varchar(50), char(3), bytea, date, time, timestamp, timestamptz, interval, uuid, json, jsonb, inet, integer[], text[], an enum type, a serial and an identity column.

## Out of scope
- Display and edit behaviour of those types (P11-25, P11-26), new features.

## Tasks
- [x] Run, list failures, fix them one by one.
- [x] Add the sample schema and its test.
- [x] Re-run until green twice in a row (a flaky test gets its cause fixed).

## Acceptance criteria
- `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres` is green; the report gives the number of tests run (not skipped) and the failures fixed.
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green with the gate off.
- No container left.

## Suggested commit message
Run and fix the PostgreSQL container tests, add the sample schema

## Report
- 22 container tests run, 0 skipped, green twice in a row (about 7 s per run, so the per-class fixture stays).
- Provider bug: `PostgresDatabaseProvider` built the data source with `NpgsqlSlimDataSourceBuilder` without `EnableArrays()`, so the schema reader failed on `text[]` (9 failures). Fixed.
- Generator bug: `CREATE INDEX "schema"."name"` is a syntax error on PostgreSQL (the index lives in the table's schema); the name is now unqualified (SQLite keeps the qualified form). Two unit-test expectations (`DdlCreateDropTests`, `PostgresAlterTests`) encoded the wrong SQL and were corrected.
- Added `tests/Shared/SampleSchema/postgres.sql` (schema `sample`) and `SampleSchemaTests`.
