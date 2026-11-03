# P11-37 - End-to-end verification: PostgreSQL

Status: todo
Depends on: P11-36, P11-17, P11-24, P11-27, P11-28, P11-30, P11-31, P11-32, P11-35

## Goal
Same as P11-36 against a PostgreSQL 17 container, plus what only a server has.

## Design decisions (final)
- The test run of P11-01 removes its containers, so add a switch `-Keep` to `eng/run-docker-tests.ps1` in this card (no test run): it starts the engine container, loads `tests/Shared/SampleSchema/postgres.sql`, prints host, port, user, password and waits for Enter before cleaning up (small, in `eng/`, documented in `docs/testing.md`). The other two verification cards use the same switch.
- Feature list: that of P11-36 on PostgreSQL, plus: connection through an SSH tunnel (with the container from P11-35 kept alive by the same switch if cheap; otherwise "not driven"), TLS require, wrong password message, schema list with `public` and a second schema, switching to another database (P11-24), routines/triggers/materialized view nodes and their definitions, `RETURNING`, `DO $$` messages tab, arrays and json cells (display and edit), `uuid` key editing, `interval`, `bytea` display, long-running `pg_sleep` cancel, terminal tab with `psql` when installed, designer on `serial`, identity, `USING` type change, ER with the `er_stress` group, completion on `public.` and quoted mixed-case identifiers.
- Output: `docs/verification/postgres.md`, fixes with regression tests, new cards for large bugs.

## Out of scope
- Features absent from the app.

## Tasks
- [ ] Add `-Keep`, drive, fix, report.

## Acceptance criteria
- Report complete (no empty row). `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; AOT publishes clean; no container or process left.
- To check by hand (owner): the report's list, max 10 steps.

## Suggested commit message
Verify PostgreSQL end to end and record the report
