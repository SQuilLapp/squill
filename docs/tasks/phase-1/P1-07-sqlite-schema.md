# P1-07 - Data.Sqlite: schema reading

Status: done
Depends on: P1-06

## Goal
Implement `ReadSchemaAsync` for SQLite, producing a `SchemaSnapshot`.

## Design decisions (final)
- `DefaultSchema = "main"` and every `TableInfo.Schema` is `"main"`. Skip names starting with `sqlite_`.
- Tables and views from `sqlite_master` (`type IN ('table','view')`). Columns via `PRAGMA table_xinfo("<name>")` (hidden/generated columns excluded: keep `hidden = 0`), primary key via the `pk` column, default via `dflt_value`.
- Indexes via `PRAGMA index_list` + `PRAGMA index_info` (skip `origin = 'pk'` auto-indexes); foreign keys via `PRAGMA foreign_key_list` grouped by `id`. Quote identifiers with `"` doubling embedded quotes.

## Out of scope
- Triggers, check constraints, virtual tables.

## Tasks
- [x] Implement the reader in `SqliteSchemaReader` (internal) and wire it to the connection.
- [x] Tests on `:memory:` with a schema containing a PK, composite index, unique index, FK, a view, a generated column.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQLite schema reading
