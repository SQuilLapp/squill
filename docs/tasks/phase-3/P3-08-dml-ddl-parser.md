# P3-08 - Sql: INSERT, UPDATE, DELETE and CREATE TABLE parsers

Status: done
Depends on: P3-07

## Goal
Cover the remaining statements whose table references matter to scope, hover and diagnostics.

## Design decisions (final)
- `InsertStatement(TableRef Target, IReadOnlyList<string> Columns, Statement? Source)` (`VALUES (..),(..)` become `ValuesStatement(IReadOnlyList<IReadOnlyList<Expression>> Rows)`), `UpdateStatement(TableRef Target, IReadOnlyList<Assignment> Set, TableSource? From, Expression? Where)`, `DeleteStatement(TableRef Target, TableSource? From, Expression? Where)` (accepts `DELETE FROM t` and T-SQL `DELETE t FROM t JOIN ...`), `CreateTableStatement(TableRef Name, IReadOnlyList<ColumnDef> Columns)` with `ColumnDef(string Name, string TypeName)` (constraints skipped to the next `,` at depth 0).
- Every other statement keeps `UnknownStatement(string? FirstKeyword)` covering the whole statement; no diagnostics for unknown-but-complete statements (`CREATE INDEX`, `ALTER`, `PRAGMA`, `SET`, ...).
- Postgres `RETURNING`, MySQL `ON DUPLICATE KEY UPDATE`, T-SQL `OUTPUT` clauses are skipped to the end of the statement without diagnostics.

## Out of scope
- Semantic checks (P3-12).

## Tasks
- [x] Implement the parsers and wire them in `SqlParser.Parse`.
- [x] Tests per statement and dialect variants listed above, plus incomplete forms (`insert into t (`, `update t set`, `delete from`).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add INSERT, UPDATE, DELETE and CREATE TABLE parsers
