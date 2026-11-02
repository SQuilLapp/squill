# P7-09 - Sql: identifier quoting and data type catalog

Status: done
Depends on: P1-04

## Goal
One quoting implementation for every generated statement, and the list of common column types per dialect for the designer's type picker.

## Design decisions (final)
- Namespace `Squill.Sql.Ddl`, folder `src/Squill.Sql/Ddl/`. `public static class SqlQuoting { string Quote(string identifier, DatabaseDialect d); string QualifiedName(string? schema, string name, DatabaseDialect d); string Literal(string value); }`. `Quote` has exactly the behaviour of the current `SqlSnippets.Quote` (`"x"`, `` `x` ``, `[x]`, closing quote doubled); `QualifiedName` omits an empty or `null` schema; `Literal` is a single-quoted string with `'` doubled.
- `SqlSnippets.Quote` in `Squill.Presentation.Editor` keeps its public signature and delegates to `SqlQuoting.Quote`; its tests stay unchanged.
- `public static class DataTypeCatalog { IReadOnlyList<string> For(DatabaseDialect d); }` returning fixed, ordered lists, each entry a valid type text for that engine: PostgreSql `smallint, integer, bigint, numeric(18,2), real, double precision, boolean, text, varchar(255), char(1), date, time, timestamp, timestamptz, uuid, jsonb, bytea`; MySql `tinyint, smallint, int, bigint, decimal(18,2), float, double, tinyint(1), text, varchar(255), char(1), date, time, datetime, timestamp, json, blob`; SqlServer `smallint, int, bigint, decimal(18,2), real, float, bit, nvarchar(max), nvarchar(255), nchar(1), date, time, datetime2, datetimeoffset, uniqueidentifier, varbinary(max)`; Sqlite `INTEGER, REAL, TEXT, BLOB, NUMERIC`.
- The catalog is a suggestion list only: the designer accepts any text.

## Out of scope
- Type mapping between engines, parsing type strings.

## Tasks
- [x] Implement both types and make `SqlSnippets.Quote` delegate.
- [x] Tests in `tests/Squill.Sql.Tests`: quoting with embedded quotes per dialect, qualified names with and without schema, `Literal`, every catalog non-empty and free of duplicates.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL quoting helpers and data type catalog
