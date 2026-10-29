# P1-14 - Data.SqlServer: schema reading

Status: done
Depends on: P1-13

## Goal
Implement `ReadSchemaAsync` for SQL Server.

## Design decisions (final)
- `DefaultSchema = schema_name()`. Exclude schemas `sys`, `INFORMATION_SCHEMA`.
- Use `sys.tables`/`sys.views`, `sys.columns` + `sys.types` (`DataType` formatted `type(max_length|precision,scale)` by a small helper, covered by a unit test), `sys.default_constraints`, `sys.indexes` + `sys.index_columns` (skip heaps), `sys.foreign_keys` + `sys.foreign_key_columns`. One query per object kind.

## Out of scope
- Procedures, functions, triggers.

## Tasks
- [x] Implement `SqlServerSchemaReader` (internal) and the type formatting helper.
- [x] Unit tests for the helper; Docker-gated schema tests.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL Server schema reading
