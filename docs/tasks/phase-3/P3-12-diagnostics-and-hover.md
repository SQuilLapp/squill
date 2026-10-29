# P3-12 - Sql: diagnostics and hover

Status: todo
Depends on: P3-11

## Goal
Surface parse errors and unknown objects, and describe objects on hover.

## Design decisions (final)
- `SqlDiagnostic(TextRange Range, DiagnosticSeverity Severity, string Message, string Code)`; `SqlDiagnosticsProvider.GetDiagnostics(SqlRequest)`: parse diagnostics become `Error` with code `SQL0001`; when `Schema` is non-null, a `TableRef` not in the schema (and not a CTE) is a `Warning` `SQL1001 Unknown table 'x'`, a qualified `ColumnRef` whose qualifier resolves to a known table lacking the column is a `Warning` `SQL1002 Unknown column 'x' in 'y'`. Unqualified columns are checked only if exactly one source is visible and it has known columns.
- `HoverInfo(TextRange Range, string Markdown)`; `SqlHoverProvider.GetHover(SqlRequest)`: on a table name => `**table** schema.name (N columns)`; on a column => `` `name`: type, nullable, PK `` ; null when nothing to say.
- Diagnostics are computed for the whole text; ranges never exceed the text.

## Out of scope
- Semantic type checking.

## Tasks
- [ ] Implement both providers.
- [ ] Tests: syntax error reporting, unknown table/column, CTE not flagged, hover on table and column, no schema => only syntax diagnostics.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL diagnostics and hover
