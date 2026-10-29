# P3-04 - Sql: statement splitter and run-under-cursor

Status: todo
Depends on: P3-03

## Goal
Split scripts into statements and find the statement at a caret offset.

## Design decisions (final)
- Namespace `Squill.Sql.Statements`. `readonly record struct SqlStatementSpan(int Start, int Length)` (trimmed of leading/trailing whitespace and comments, includes the terminating `;`). `static class SqlStatementSplitter { IReadOnlyList<SqlStatementSpan> Split(string text, DatabaseDialect d); SqlStatementSpan? GetStatementAt(string text, int offset, DatabaseDialect d); }`.
- Split on `;` tokens only (so semicolons inside strings, comments and dollar-quoted bodies never split). Comment-only or empty statements are dropped.
- T-SQL: a line containing only `GO` (any case, optional repeat count) is a batch separator and is not part of any statement. MySQL: a line `DELIMITER <x>` changes the terminator until the next `DELIMITER` line (the directive lines are not statements). Compound bodies: when a statement starts with `CREATE` and contains `TRIGGER`, `PROCEDURE` or `FUNCTION` before its first `BEGIN`, track `BEGIN`/`CASE` ... `END` nesting and do not split on `;` while depth > 0 (a `CASE ... END` closes with its own END).
- `GetStatementAt`: the statement containing `offset`; if `offset` sits in whitespace/comments between statements, the closest previous statement; null if the text has none.

## Out of scope
- Parsing semantics.

## Tasks
- [ ] Implement the splitter.
- [ ] Tests: plain scripts, `;` in strings/comments, Postgres `$$` function body, T-SQL `GO`, MySQL `DELIMITER //` procedure, SQLite `CREATE TRIGGER ... BEGIN ...; ...; END;`, caret positions (start, end, between statements, empty text).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL statement splitter and run-under-cursor lookup
