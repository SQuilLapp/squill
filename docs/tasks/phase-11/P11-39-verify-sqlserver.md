# P11-39 - End-to-end verification: SQL Server

Status: todo
Depends on: P11-37, P11-19, P11-24, P11-27, P11-28, P11-30, P11-31, P11-32

## Goal
Same as P11-37 against a SQL Server 2022 container (Podman machine at 4096 MiB).

## Design decisions (final)
- Start the container with `-Keep` (P11-37). Feature list: that of P11-36 plus SQL Server specifics: login with `sa`, wrong password message, certificate trust options (P11-08: with trust off and an untrusted certificate the error message must be readable), schemas (`dbo` and a second), switching database, routines, triggers and view definitions, `EXEC` of a procedure with two result sets and `PRINT` (Messages tab), `GO` batches in a script (run all), `OUTPUT` and identity read-back with a trigger table (P11-33), `uniqueidentifier`, `datetimeoffset`, `money`, `xml`, `rowversion` read-only cells, `nvarchar` Unicode, `TOP`/`OFFSET` paging, `WAITFOR DELAY` cancel, designer (default constraint drop, `sp_rename`, identity add refusal), ER with `er_stress`, completion with `[brackets]`, terminal `sqlcmd` when installed, `USE`-less database switching.
- Output: `docs/verification/sqlserver.md`, fixes with regression tests, new cards for large bugs.

## Out of scope
- Azure SQL, Windows authentication, features absent from the app.

## Tasks
- [ ] Drive, fix, report.

## Acceptance criteria
- Report complete. `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; AOT publishes clean; no container or process left.
- To check by hand (owner): the report's list, max 10 steps.

## Suggested commit message
Verify SQL Server end to end and record the report
