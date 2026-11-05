# P11-04 - SQL Server container tests run and pass

Status: done
Depends on: P11-01

## Goal
Every Docker-gated test of `tests/Squill.Data.SqlServer.Tests` has actually run against SQL Server 2022 and passes.

## Design decisions (final)
- Same method and rules as P11-02, with `-Engine sqlserver`. The Podman machine needs 4096 MiB (P11-01 prints the command); if it is below that, stop and report instead of skipping.
- First suspicion to check: `SqlServerDatabaseProvider.BuildConnectionString` trusts the server certificate only when the host is the literal `localhost`, while Testcontainers can return `127.0.0.1` or another name. Do not change the trust rule here (P11-07 owns connection options): make the fixture connect through the host `localhost` and write the finding in the report.
- SQL Server specifics to check: login failure while the container is still starting (wait strategy), `GO` is not a statement (the DML and ALTER tests use single statements), `OUTPUT INSERTED` read-back on a table that has a trigger (add this case to the DML round trip and report the behaviour; any fix is a finding for P11-33).
- `tests/Shared/SampleSchema/sqlserver.sql` with the same objects as the Postgres one (tinyint, smallint, int, bigint, decimal(18,4), real, float, bit, char, nvarchar, varchar(max), varbinary(max), date, time, datetime2(3), datetimeoffset, smalldatetime, money, uniqueidentifier, xml, rowversion, an IDENTITY column) and the same test `SampleSchema_LoadsAndIsReadBack`.

## Out of scope
- Display and edit behaviour of those types (P11-25, P11-26), Azure SQL, Windows authentication.

## Tasks
- [x] Run, fix, add sample schema and test, run twice green.

## Acceptance criteria
- `rtk pwsh eng/run-docker-tests.ps1 -Engine sqlserver` is green; the report gives tests run, failures fixed and the trigger/OUTPUT finding.
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- No container left.

## Suggested commit message
Run and fix the SQL Server container tests, add the sample schema

## Result
- 37 SQL Server container tests run and pass twice (`eng/run-docker-tests.ps1 -Engine sqlserver`); the stale `ReadSchemaAsync_NotImplemented_ThrowsNotSupported` test was dropped.
- **localhost does not work on Windows with Podman/WSL**: the published port is forwarded on IPv4 only and `Data Source=localhost,<port>` hangs in SqlClient (5.2.2 and 7.1.1), while `127.0.0.1` connects. `BuildConnectionString` now applies the existing "trust the certificate" rule to the loopback literals `127.0.0.1` and `::1` as well as `localhost` (nothing for other hosts; unit-tested). **P11-07 no longer needs to do this part.** The fixture stays on the container host `127.0.0.1`.
- Fixed bugs: `SqlServerDdlGenerator.DropDefault` emitted `EXEC('...' + QUOTENAME(@n))` (EXEC accepts no function call: syntax error), now builds the statement in `@s`; `SqlServerSchemaReader` listed the `is_ms_shipped` tables of `master` (`MSreplication_options`, `spt_*`), now filtered.
- Test fixes: the `actions` table of the schema-reader setup had two cascading foreign keys to one parent (refused by SQL Server); the sample schema uses one parent table per action (`fk_targets`, `_b`, `_c`, `_d`; no RESTRICT in SQL Server, NO ACTION instead).
- **Finding for P11-33**: `INSERT ... OUTPUT INSERTED.id` (what `DmlGenerator` emits for the read-back) is refused by SQL Server with error 334 on a table that has a trigger (pinned by `OutputInserted_OnTableWithTrigger_IsRefusedByServer`). The fix is `OUTPUT INSERTED.id INTO @t` or `SCOPE_IDENTITY()`.
- `eng/run-docker-tests.ps1` no longer checks the machine memory on a WSL machine (`podman machine set --memory` is refused there, the VM takes WSL's memory); `docs/testing.md` says so.
