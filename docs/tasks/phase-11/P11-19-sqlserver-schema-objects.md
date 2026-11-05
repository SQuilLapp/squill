# P11-19 - Data.SqlServer: routines, triggers, view definitions

Status: done
Depends on: P11-15, P11-04

## Goal
The SQL Server reader returns functions, stored procedures, triggers and view definitions.

## Design decisions (final)
- Views: `sys.sql_modules.definition` joined to `sys.views` (null when the module is encrypted; keep null).
- Routines: `sys.objects` with `type in ('P','FN','IF','TF')` joined to `sys.sql_modules`, schema filter as in the existing reader (`sys` and `INFORMATION_SCHEMA` out, `is_ms_shipped = 0`); `Signature` = `name(` + parameters from `sys.parameters` + `sys.types` (`@p type`, skipping parameter 0 which is the return value) + `)`; `ReturnType` from parameter 0 or `TABLE` for `IF`/`TF`.
- Triggers: `sys.triggers` (table triggers only, `parent_class = 1`), `Timing` = `INSTEAD OF` when `is_instead_of_trigger = 1` else `AFTER`, `Events` from `sys.trigger_events`, `Definition` from `sys.sql_modules`.
- Docker-gated tests in `SqlServerSchemaReaderTests`: a scalar function, an inline table function, a procedure, a trigger, a view; add to `tests/Shared/SampleSchema/sqlserver.sql`.

## Out of scope
- UI, CLR objects, synonyms, sequences.

## Tasks
- [x] Queries, mapping, tests, sample schema update.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine sqlserver` green; no container left.

## Suggested commit message
Read SQL Server routines, triggers and view definitions
