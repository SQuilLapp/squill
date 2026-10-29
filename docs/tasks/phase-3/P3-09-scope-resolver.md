# P3-09 - Sql: scope analysis at a caret position

Status: todo
Depends on: P3-08

## Goal
Tell completion and diagnostics which tables, aliases and CTEs are visible at an offset and in which clause the caret is.

## Design decisions (final)
- Namespace `Squill.Sql.Analysis`. `static class SqlScopeResolver { ScopeInfo Resolve(SqlScript script, int offset); }`.
- `ScopeInfo(ClauseKind Clause, IReadOnlyList<VisibleSource> Sources)`; `enum ClauseKind { None, Select, From, Join, On, Where, GroupBy, Having, OrderBy, Set, Values, Other }`; `VisibleSource(string? Schema, string Name, string? Alias, IReadOnlyList<string>? DerivedColumns)` where `DerivedColumns` is null for real tables and, for CTEs and derived tables, the output names of the inner select (alias, else the last part of a `ColumnRef`; `*` yields `null` meaning "unknown").
- The innermost `SelectStatement`/`UpdateStatement`/`DeleteStatement`/`InsertStatement` whose range contains `offset` defines the clause; sources of enclosing statements are included after the inner ones (correlated subqueries); CTEs declared by `WITH` are visible in the main query and in later CTEs.
- An `offset` at the end of an incomplete statement counts as inside it (`select a.| from t`, `select | from t`, `select * from t where |`).

## Out of scope
- Schema lookups (P3-11).

## Tasks
- [ ] Implement the resolver.
- [ ] Tests: alias visibility, joins, CTE and derived table columns, correlated subquery, clause detection for each `ClauseKind`, incomplete statements.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL scope resolver
