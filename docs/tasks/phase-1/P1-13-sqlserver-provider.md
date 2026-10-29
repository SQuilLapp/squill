# P1-13 - Data.SqlServer: provider and AOT probe

Status: todo
Depends on: P1-12

## Goal
SQL Server provider on Microsoft.Data.SqlClient, with an honest AOT verdict (risk R3, highest).

## Design decisions (final)
- Project `src/Squill.Data.SqlServer`, class `SqlServerDatabaseProvider`. Package `Microsoft.Data.SqlClient` 7.1.1. Connection string via `SqlConnectionStringBuilder` (`DataSource = host,port`, port default 1433, `TrustServerCertificate` false unless `Host` is `localhost`).
- Parameters bound as `@name`. Reader mapping as in P1-06. `ReadSchemaAsync` throws `NotSupportedException("P1-14")`.
- Tests in `tests/Squill.Data.SqlServer.Tests`, Docker-gated with `Testcontainers.MsSql` 4.16.0; non-gated test for the connection string.
- `tools/Squill.AotProbe.SqlServer` sets `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (the only project allowed to): every IL2xxx/IL3xxx warning raised by the package is listed in the report, not fixed. If the AOT-published probe fails at runtime, the verdict is `incompatible` and the card still completes.
- If `Squill.Data.SqlServer` itself raises AOT warnings, set `<IsAotCompatible>false</IsAotCompatible>` in its csproj with a comment pointing to the report, and keep `-warnaserror` green.

## Out of scope
- Schema reading (P1-14).
- Entra/Azure auth, Always Encrypted.

## Tasks
- [ ] Create provider, tests, probe; update solution and `Directory.Packages.props`.
- [ ] Publish the probe, run it against Docker SQL Server if available, write the report row with caveats.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish tools/Squill.AotProbe.SqlServer -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add SQL Server provider and AOT probe
