# P11-18 - Data.MySql: routines, triggers, view definitions

Status: todo
Depends on: P11-15, P11-03

## Goal
The MySQL reader returns stored functions and procedures, triggers and view definitions.

## Design decisions (final)
- Views: `information_schema.VIEWS.VIEW_DEFINITION` into `TableInfo.Definition`.
- Routines: `information_schema.ROUTINES` where `ROUTINE_SCHEMA = database()`; `Signature` built from `information_schema.PARAMETERS` (name and `DTD_IDENTIFIER`, ordered by `ORDINAL_POSITION`, position 0 is the return value); `Definition` from `show create function|procedure` is too slow for many routines: use `ROUTINE_DEFINITION` (body) and say so in the `<remarks>`; the explorer script action (P11-20) wraps it in a `CREATE` header built from the signature.
- Triggers: `information_schema.TRIGGERS` (`ACTION_TIMING`, `EVENT_MANIPULATION`, `ACTION_STATEMENT`).
- Docker-gated tests in `MySqlSchemaReaderTests`: a function, a procedure with two parameters, a trigger, a view; add them to `tests/Shared/SampleSchema/mysql.sql`. Note the result columns are upper-case under MySQL 8 (checked in P11-03).

## Out of scope
- UI, events, MariaDB.

## Tasks
- [ ] Queries, mapping, tests, sample schema update.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine mysql` green; no container left.

## Suggested commit message
Read MySQL routines, triggers and view definitions
