# P1-06 - Data.Sqlite: provider (connect, execute, stream)

Status: done
Depends on: P1-05

## Goal
First real provider, used as the reference implementation and by most later integration tests.

## Design decisions (final)
- Project `src/Squill.Data.Sqlite`, namespace `Squill.Data.Sqlite`, class `SqliteDatabaseProvider : IDatabaseProvider`. Package `Microsoft.Data.Sqlite` 11.0.0-rc.3 (if it does not restore, use 10.0.14 and note it in `docs/architecture.md`). Tests in `tests/Squill.Data.Sqlite.Tests`.
- Connection string built with `SqliteConnectionStringBuilder { DataSource = profile.FilePath }`; a null/empty `FilePath` throws `ArgumentException`. `password` is ignored (no encryption support in v1).
- `ExecuteAsync` uses `SqliteCommand` + `ExecuteReaderAsync`; parameters bound as `@name` (the `Name` carries no prefix). `ReadBatchAsync` reads up to `maxRows` rows, converting `DBNull` to `null`; `ColumnInfo.DataTypeName` from `GetDataTypeName` (empty string allowed), `ClrType` from `GetFieldType`, `IsNullable` is always true.
- `RecordsAffected` is read from the reader after the last batch. Cancellation: check the token between rows; pass it to the async calls.
- `ReadSchemaAsync` throws `NotSupportedException("P1-07")` until the next card.

## Out of scope
- Schema reading (P1-07).
- Encryption, extensions, attach.

## Tasks
- [x] Create the project, provider, connection and stream classes.
- [x] Add the project and its test project to `Squill.slnx` and `Directory.Packages.props`.
- [x] Tests on `:memory:`: create/insert/select with typed values and NULL, parameters, `RecordsAffected` for an insert, batching with `maxRows = 2`, cancellation, error on invalid SQL surfaces `SqliteException`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQLite provider
