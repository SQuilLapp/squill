# P3-07 - Sql: SELECT statement parser

Status: done
Depends on: P3-06

## Goal
Parse queries into a tree sufficient for scope analysis, completion and diagnostics.

## Design decisions (final)
- `SelectStatement(IReadOnlyList<CommonTableExpr> Ctes, bool Distinct, IReadOnlyList<SelectItem> Items, TableSource? From, Expression? Where, IReadOnlyList<Expression> GroupBy, Expression? Having, IReadOnlyList<OrderItem> OrderBy, Expression? Limit, Expression? Offset, Expression? Top)`; `SetOperation(SetOperator Op, bool All, Statement Left, Statement Right)` for `UNION`/`INTERSECT`/`EXCEPT`; `CommonTableExpr(string Name, IReadOnlyList<string> Columns, Statement Query)`; `SelectItem(Expression Value, string? Alias)`.
- Table sources: `TableRef(string? Schema, string Name, string? Alias)` (3-part names keep the last two), `SubquerySource(Statement Query, string? Alias)`, `JoinSource(TableSource Left, JoinKind Kind, TableSource Right, Expression? On)` (`INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`, comma join); optional `AS` before aliases; T-SQL `TOP n` fills `Top`; other dialects use `LIMIT`/`OFFSET`; T-SQL `OFFSET .. FETCH` is parsed into `Offset` only.
- Public entry: `SqlParser.Parse(string text, DatabaseDialect d)` returning `SqlScript(IReadOnlyList<Statement> Statements, IReadOnlyList<ParseDiagnostic> Diagnostics)`; statements come from `SqlStatementSplitter.Split`; non-SELECT statements are `UnknownStatement` until P3-08. `SubqueryExpr.Query` becomes `Statement` (adapt P3-06).
- Tolerance: `select a. from t` and `select from t` still produce a `SelectStatement` with `From` set; a missing table after `FROM` yields an empty `TableRef` plus a diagnostic.

## Out of scope
- INSERT/UPDATE/DELETE/DDL (P3-08).

## Tasks
- [x] Implement the select parser.
- [x] Tests: CTEs, joins with aliases, subquery in FROM and in WHERE, UNION, T-SQL TOP vs LIMIT, incomplete statements for each tolerance case, node ranges.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SELECT parser with error recovery
