# P12-06 - Data: variable substitution proven on the four engines

Status: todo
Depends on: P12-05, P11-02, P11-03, P11-04

## Goal
The text produced by P12-05 really runs on SQLite, PostgreSQL, MySQL and SQL Server, including the injection cases.

## Design decisions (final)
- Integration tests drive `VariableSubstitutor` and a real `IDatabaseConnection` (not the query tab): for each engine, one test class in the existing `tests/Squill.Data.<Driver>.Tests` projects (SQLite also in `tests/Squill.Presentation.Tests` if Presentation cannot be referenced from the driver test projects; check the dependency graph in `docs/tasks/README.md` and place the shared cases in a Presentation-level test project that references the providers, creating `tests/Squill.Presentation.Engines.Tests` copied from an existing test csproj only if none exists).
- Shared sample schema: `tests/Shared/SampleSchema/<engine>.sql` (phase 11); never duplicate it.
- Cases per engine: `WHERE id = {{id}}` with a number and with a text value on a numeric column (driver conversion error is reported as a message, not a crash), `{{name}}` with a quote in the value (`O'Brien`), the two hostile values of spec section 8 (`1; DROP TABLE ...; --`, `' OR '1'='1`) return data or nothing and the table still exists, `FROM {{ident:table}}` with a plain name, a `schema.table` name, a name needing quotes (space, upper case) and a hostile one (must fail with "no such table" style error, never execute the tail), `LIMIT`/`TOP` with `{{raw:n}}` when the variable allows raw (engine syntax: `LIMIT {{raw:n}}`, `SELECT TOP ({{raw:n}})`), a bound variable together with a user `@p` parameter, a NULL result row.
- Docker-gated tests use the phase 11 gate and runner (`rtk pwsh eng/run-docker-tests.ps1 -Engine all`); SQLite never skips.

## Out of scope
- UI.

## Tasks
- [ ] Tests above on the four engines; fix any bug found if under about 30 lines with a regression test, otherwise write a new card `P12-16` onwards and add it to the index.

## Acceptance criteria
- Build 0 warnings; default test run green (SQLite included); `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green with no container left (`podman ps -a`); a skipped Docker test is never reported as verified.

## Suggested commit message
Prove variable substitution and injection safety on every engine
