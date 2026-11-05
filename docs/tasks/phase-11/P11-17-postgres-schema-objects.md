# P11-17 - Data.Postgres: routines, triggers, view definitions, materialized views

Status: done
Depends on: P11-15, P11-02

## Goal
The PostgreSQL reader returns functions, procedures, triggers, view definitions, and tells materialized views apart (today `relkind 'm'` is read like a view).

## Design decisions (final)
- `relkind 'm'` -> `TableKind.MaterializedView`; `Definition` from `pg_get_viewdef(c.oid, true)` for `v` and `m`.
- Routines: `pg_proc` joined to `pg_namespace` with the same `UserSchemas` filter, `prokind in ('f','p')` (aggregates and window functions skipped), `Signature` = `proname || '(' || pg_get_function_identity_arguments(oid) || ')'`, `ReturnType` = `pg_get_function_result(oid)` (null for procedures), `Definition` = `pg_get_functiondef(oid)`. Exclude functions that belong to extensions (`pg_depend` with `deptype = 'e'`).
- Triggers: `pg_trigger` where `not tgisinternal`, `Timing` BEFORE/AFTER/INSTEAD OF from `tgtype` bits, `Events` joined INSERT/UPDATE/DELETE/TRUNCATE, `Definition` = `pg_get_triggerdef(oid, true)`.
- Extend `PostgresSchemaReaderTests` (Docker-gated) with: one function in `public`, one procedure, an overloaded function (two snapshot rows), a trigger, a plain view and a materialized view; add the matching objects to `tests/Shared/SampleSchema/postgres.sql`.

## Out of scope
- UI, sequences, types, extensions.

## Tasks
- [x] Queries, mapping, tests, sample schema update.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres` green; no container left.

## Suggested commit message
Read PostgreSQL routines, triggers and view definitions
