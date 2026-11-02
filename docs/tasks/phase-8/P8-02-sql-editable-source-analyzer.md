# P8-02 - Sql: editable source analysis

Status: done
Depends on: P8-01, P3-07, P1-04

## Goal
Decide whether a result grid may be edited and, if so, which table and key it maps to, from the SQL text that produced it.

## Design decisions (final)
- Namespace `Squill.Sql.Editing`, `public static class EditableSourceAnalyzer { public static EditableSourceResult Analyze(string sql, DatabaseDialect dialect, SchemaSnapshot schema, IReadOnlyList<ColumnInfo> resultColumns); }` and `public sealed record EditableSourceResult(EditTarget? Target, string? Reason)` (exactly one of the two is non-null; `Reason` is shown to the user as "Read-only: <reason>").
- Parse with `SqlParser.Parse(sql, dialect)`. Reasons, checked in this order (use these exact texts): script has diagnostics or not exactly one statement -> `not a single statement`; statement is not a `SelectStatement` (set operations included) -> `not a plain SELECT`; `Ctes` non-empty, `Distinct`, `GroupBy` non-empty or `Having` set -> `query is aggregated or uses WITH/DISTINCT`; `From` is null, a `JoinSource` or a `SubquerySource` -> `query does not read a single table`; the `TableRef` is not found with `SchemaSnapshot.FindTable` (read the method in `src/Squill.Core/Schema/SchemaSnapshot.cs`) or is a view -> `source is not a table`; the table has no primary key -> `table has no primary key`; two result columns share a name (case-insensitive) -> `duplicate column names`; a key column is not among the mapped result columns -> `primary key column <name> is not selected`.
- `WHERE`, `ORDER BY`, `LIMIT`, `OFFSET` and T-SQL `TOP` are allowed.
- Column mapping: the "plain names" are the table's column names when a `StarExpr` without qualifier (or with the table name or alias as qualifier) is in the select list, plus the names of bare `ColumnRef` items whose alias is null or equals their name (case-insensitive). A result column whose name (case-insensitive) is in the plain names and matches a table column becomes an `EditColumn`; every other result column is read-only (absent from `Target.Columns`). `EditColumn.Definition` uses the table's own spelling.
- Pure, synchronous, no I/O.

## Out of scope
- Joins, views, updatable views, tables without a primary key (no ROWID/ctid tricks), editing aggregated or computed columns.

## Tasks
- [x] Implement the analyzer.
- [x] Tests in `tests/Squill.Sql.Tests` over a small hand-built `SchemaSnapshot` for all four dialects: `SELECT *`, explicit columns, quoted names, schema-qualified table, alias on the table, `TOP` (SQL Server) and `LIMIT`, `WHERE`/`ORDER BY`; every reason above once; a computed column is read-only while the rest stays editable; an alias renaming a column becomes read-only.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add editable source analysis
