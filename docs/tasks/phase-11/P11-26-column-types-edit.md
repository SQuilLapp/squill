# P11-26 - Column types edited and written back correctly on every engine

Status: done
Depends on: P11-25

## Goal
Editing a cell of every supported type and committing writes the intended value, and unsupported types are read-only with a clear reason; P8-13 covered `int`, `long`, `decimal`, `string`, `bool`, `DateTime`, `byte[]`, `Guid` only.

## Design decisions (final)
- For each column of the sample all-types tables: `CellValueParser` parses the displayed text of P11-25 back to the CLR value the driver accepts (`TimeSpan`, `DateOnly`, `TimeOnly`, `DateTimeOffset`, `Guid`, arrays for PostgreSQL via `string[]`/`int[]` parameters only for `integer[]` and `text[]`, JSON as `string` with an explicit cast in the generated SQL: `@p::jsonb` on PostgreSQL, `cast(@p as json)` is not needed on MySQL (string accepted), SQL Server `xml` accepts string); the cast table lives in `DmlGenerator` per dialect, tested in `Squill.Sql.Tests`.
- Round trip test per driver (`ColumnTypeEditTests`, SQLite never skipped, others Docker-gated): for every editable column insert a row through `DmlGenerator` with the parsed value, read back, assert `CellFormatter.Format` equals the input text (canonical forms in the test).
- Types that cannot be edited safely (`rowversion`, computed columns, generated columns, PostgreSQL `GENERATED ALWAYS AS IDENTITY` value, `sql_variant`, `hierarchyid`, `geography`, ranges, composite types, MySQL generated columns) are read-only: `EditableGridDataSource` reports a reason per column, the cell editor refuses with that reason (catalog key per reason, fr/en). Column metadata for generated/computed is read by the schema readers: extend `ColumnDefinition` only if the information is missing (add `IsGenerated` defaulted false, fill it in the four readers, test it).
- Parser failure keeps the existing "cannot parse" issue flow.

## Out of scope
- New editors (calendar, JSON editor), spatial types.

## Tasks
- [x] Parser, DML casts, generated-column flag, read-only reasons, tests.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.
- To check by hand: on each head, edit one cell of type uuid, timestamp, json and a read-only one (see P11-36 ... P11-39 reports).

## Suggested commit message
Edit and write back every column type, mark generated columns read-only
