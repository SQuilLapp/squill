# P8-13 - Data providers: DML round-trip tests on every dialect

Status: todo
Depends on: P8-04, P8-08, P1-14

## Goal
Prove the generated INSERT, UPDATE and DELETE statements and the generated-key read-back against real servers; SQLite runs everywhere, the others when Docker tests are enabled.

## Design decisions (final)
- One test class `DmlRoundTripTests` in each of `tests/Squill.Data.Sqlite.Tests`, `tests/Squill.Data.Postgres.Tests`, `tests/Squill.Data.MySql.Tests`, `tests/Squill.Data.SqlServer.Tests` (add a project reference to `Squill.Sql` where missing). The Docker ones use the existing container fixtures and `DockerGate` of those projects (`[Fact(SkipUnless = ..., SkipType = ...)]`, copy the pattern of the existing schema-reader tests). The Sqlite one uses a temp file and is never skipped.
- The shared scenario (written once per class, same table shape adapted to the dialect, created with `DdlGenerators.For(dialect).CreateTable`): table `items(id auto-increment primary key, name text not null, qty integer null, price decimal(10,2) null)` plus a second table with a composite key `(a, b)`. Steps: `DmlGenerator` + `PlanInsert` insert with generated key (read the key back as the committer will: from the insert result, or from `LAST_INSERT_ID()` for MySQL), insert with an explicit key, insert with no values where all columns have defaults (table `defaults_only`), update of one column by key, update of a key column, update setting NULL, delete by key, composite-key update and delete, a `NULL` value in the key predicate on SQLite only; after each step read the table back with a plain `SELECT` and compare. Statements run through `IDatabaseConnection.ExecuteAsync(sql, parameters, ct)` with `DmlParameter` values mapped to `QueryParameter`.
- Typed parameter values: `int`, `long`, `decimal`, `string`, `bool`, `DateTime`, `byte[]`, `Guid` (where the dialect has a column type for them) are bound as CLR values and read back equal; a failure caused by a provider that does not accept one of these types is **reported as a finding in the card report and the type is documented as unsupported in P8-18**, never worked around by inlining values into SQL.

## Out of scope
- Presentation types (P8-09 has its own end-to-end test), performance.

## Tasks
- [ ] Write the four test classes.
- [ ] Run the Docker ones locally when Docker is available (`SQUILL_DOCKER_TESTS=1`); if not, state in the card report that they were not run. Stop every container you started.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- No container or process left running.

## Suggested commit message
Add DML round-trip tests for every dialect
