# P3-02 - Sql: PostgreSQL and SQLite tokenizers

Status: done
Depends on: P3-01

## Goal
Dialect-specific lexing for PostgreSQL and SQLite.

## Design decisions (final)
- `PostgresTokenizer`: dollar-quoted strings `$$...$$` and `$tag$...$tag$` (one `StringLiteral` token), `E'...'` strings with backslash escapes, nested block comments, `::` operator, `$1` parameters (`Parameter`), `U&"..."` identifiers; extra keywords (`RETURNING`, `ILIKE`, `LATERAL`, `OVER`, `WINDOW`, ...) in `Keywords/PostgresKeywords.cs`.
- `SqliteTokenizer`: `[ident]` and `` `ident` `` as `QuotedIdentifier`, parameters `?`, `?NNN`, `:name`, `@name`, `$name` (`Parameter`), `--` and `/* */` comments, keywords (`PRAGMA`, `AUTOINCREMENT`, `WITHOUT`, `ROWID`, ...) in `Keywords/SqliteKeywords.cs`.
- `SqlTokenizers.For` returns these for `PostgreSql` and `Sqlite`. The contiguity invariant of P3-01 holds for both (rerun the fuzz test per dialect via a shared `[Theory]`).

## Out of scope
- MySQL and T-SQL (P3-03).

## Tasks
- [x] Implement both tokenizers and keyword sets; extend `SqlTokenizers.For`.
- [x] Tests for each special form, unterminated forms, and the fuzz invariant for both dialects.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add PostgreSQL and SQLite tokenizers
