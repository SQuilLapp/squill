# P1-12 - Data.MySql: schema reading

Status: done
Depends on: P1-11

## Goal
Implement `ReadSchemaAsync` for MySQL/MariaDB.

## Design decisions (final)
- MySQL has no schema level inside a database: `DefaultSchema = database()`, `TableInfo.Schema` is the database name, only the connected database is read.
- Use `information_schema.TABLES`, `COLUMNS` (`COLUMN_TYPE` as `DataType`, `COLUMN_KEY = 'PRI'`), `STATISTICS` (indexes, grouped by `INDEX_NAME` ordered by `SEQ_IN_INDEX`), `KEY_COLUMN_USAGE` joined to `REFERENTIAL_CONSTRAINTS` for FKs. One query per object kind.

## Out of scope
- Routines, events, triggers.

## Tasks
- [x] Implement `MySqlSchemaReader` (internal).
- [x] Docker-gated tests: PK, composite unique index, FK, view.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add MySQL schema reading
