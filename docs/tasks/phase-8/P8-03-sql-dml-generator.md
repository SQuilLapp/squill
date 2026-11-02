# P8-03 - Sql: INSERT, UPDATE and DELETE generation

Status: todo
Depends on: P8-01, P7-09

## Goal
Generate parameterized INSERT, UPDATE and DELETE statements for the four dialects from a `RowEdit` and an `EditTarget`, using the primary key.

## Design decisions (final)
- Namespace `Squill.Sql.Editing`. `public sealed record DmlStatement(string Sql, IReadOnlyList<QueryParameter> Parameters)` (`QueryParameter` is in `Squill.Data`). `Squill.Sql` references only `Squill.Core` today: to avoid a new dependency, **move nothing**; define instead `public sealed record DmlParameter(string Name, object? Value)` in `Squill.Sql.Editing` and let the Presentation layer map it to `QueryParameter`. `DmlStatement(string Sql, IReadOnlyList<DmlParameter> Parameters)`. `public static class DmlGenerator { public static DmlStatement Generate(RowEdit edit, EditTarget target, DatabaseDialect dialect); }`.
- Parameters are named `p0`, `p1`, ... in order of appearance and written as `@p0` in the SQL for **all** dialects (the providers bind `@name`; see `ParameterScanner.Rewrite`). Values are passed as given (already typed by P8-05), never inlined into the text. All identifiers go through `SqlQuoting.QualifiedName` / `SqlQuoting.Quote`.
- INSERT: `INSERT INTO <table> (<cols>) VALUES (@p0, ...)` in the order of the values given. No values at all: PostgreSql, SqlServer and Sqlite use `INSERT INTO <table> DEFAULT VALUES`, MySql uses `INSERT INTO <table> () VALUES ()`. Generated-key read-back is added by P8-04, not here.
- UPDATE: `UPDATE <table> SET <col> = @pN, ... WHERE <key predicate>` with the changed columns in the order of `Changes`. DELETE: `DELETE FROM <table> WHERE <key predicate>`. Key predicate: key columns joined by ` AND `; each is `<col> = @pN`, or `<col> IS NULL` (no parameter) when its key value is `null` (SQLite allows NULL in key columns).
- No dialect uses `LIMIT`/`TOP` on UPDATE or DELETE: the key is unique, and the committer (P8-09) checks the affected-row count instead.
- Validation (throw `ArgumentException`): an `UpdateRow` or `DeleteRow` whose `Key` does not hold exactly the `KeyColumns` of the target in order, an `UpdateRow` with empty `Changes`, a column that is not in `Target.Columns`.

## Out of scope
- Reading generated keys back (P8-04), type conversion of values (P8-05), several rows in one statement.

## Tasks
- [ ] Implement `DmlParameter`, `DmlStatement`, `DmlGenerator`.
- [ ] Tests in `tests/Squill.Sql.Tests` with exact expected strings and parameter lists per dialect: insert with values, insert with no values, update of one and of several columns, update of a key column, composite key, NULL key value, delete, schema qualification, quoting of identifiers with embedded quotes, every `ArgumentException` case.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add INSERT, UPDATE and DELETE generation
