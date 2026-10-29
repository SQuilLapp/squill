# P1-11 - Data.MySql: provider and AOT probe

Status: done
Depends on: P1-10

## Goal
MySQL/MariaDB provider on MySqlConnector, proven under Native AOT (risk R3).

## Design decisions (final)
- Project `src/Squill.Data.MySql`, class `MySqlDatabaseProvider`. Package `MySqlConnector` 2.6.2. `MySqlConnectionStringBuilder` from the profile (port default 3306).
- Parameters bound as `@name`. Reader mapping as in P1-06. `ReadSchemaAsync` throws `NotSupportedException("P1-12")`.
- Tests in `tests/Squill.Data.MySql.Tests`, Docker-gated with `Testcontainers.MySql` 4.16.0 (same gating as P1-09); non-gated test for the connection string.
- `tools/Squill.AotProbe.MySql` as in P1-09 (`select 1 as a, 'x' as b`), add the row to `docs/poc/aot-drivers.md`.

## Out of scope
- Schema reading (P1-12).
- MariaDB-specific features.

## Tasks
- [x] Create provider, tests, probe; update solution and `Directory.Packages.props`.
- [x] Publish the probe; record the verdict.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish tools/Squill.AotProbe.MySql -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add MySQL provider and AOT probe
