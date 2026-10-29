# P1-09 - Data.Postgres: provider and AOT probe

Status: todo
Depends on: P1-08

## Goal
PostgreSQL provider on Npgsql, proven under Native AOT (risk R3).

## Design decisions (final)
- Project `src/Squill.Data.Postgres`, class `PostgresDatabaseProvider`. Package `Npgsql` 10.0.3. Build the data source with `NpgsqlSlimDataSourceBuilder` (no reflection-based plugins) and `.Build()`; one data source per `OpenAsync`, disposed with the connection.
- Connection string from the profile (`Host`, `Port` default 5432, `Database`, `UserName`) plus `password` argument. Parameters bound as `@name` via `NpgsqlParameter`. Reader to `ColumnInfo`/batches as in P1-06 (`GetDataTypeName` gives the PG type name).
- `ReadSchemaAsync` throws `NotSupportedException("P1-10")` for now.
- Integration tests in `tests/Squill.Data.Postgres.Tests` use `Testcontainers.PostgreSql` 4.16.0, gated by env var `SQUILL_DOCKER_TESTS=1` through xunit.v3 `[Fact(SkipUnless = ..., SkipType = ...)]` with a shared helper in the test project. A non-gated unit test checks the connection string builder.
- `tools/Squill.AotProbe.Postgres`: args `<connection string>`; connects, runs `select 1 as a, 'x' as b`, prints `OK`. Add the row to `docs/poc/aot-drivers.md`; if no server/Docker is available, the runtime check column says `not run` and the verdict is based on publish warnings only.

## Out of scope
- Schema reading (P1-10).
- Plugins (NodaTime, PostGIS, JSON mapping).

## Tasks
- [ ] Create provider, tests, probe; add to solution and `Directory.Packages.props`.
- [ ] Publish the probe; record IL warnings (if any) and the verdict.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish tools/Squill.AotProbe.Postgres -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- With Docker available: `SQUILL_DOCKER_TESTS=1 rtk bypass dotnet test --solution Squill.slnx -c Release` is green.

## Suggested commit message
Add PostgreSQL provider and AOT probe
