# P21-22 - Sql: symbol resolution for go to definition and find usages

Status: todo
Depends on: none

## Goal
Given a caret offset in a script, tell what the name under the caret is (table, column, alias, CTE, derived table / sub-query column) and where it is defined and used, as DataGrip's context-aware navigation does.

Exists today: `SqlScopeResolver.Resolve(script, offset)` returns the clause and the visible sources (`VisibleSource`: schema, name, alias, derived columns); `SqlTreeWalker.References(script)` (internal) lists every `TableRef` / `ColumnRef` with the visible CTEs and select aliases (used by the diagnostics); `SqlHoverProvider` describes a name. Nothing returns a definition position or a usage list.

## Design decisions (final)
- New `Squill.Sql/Analysis/SqlSymbolResolver` (public static) with `SymbolAt(SqlScript script, int offset, SchemaSnapshot? schema) -> SqlSymbol?` and `Usages(SqlScript script, SqlSymbol symbol) -> IReadOnlyList<TextRange>`.
- `SqlSymbol` record: `Kind` (`Table`, `View`, `Column`, `Alias`, `Cte`, `DerivedTable`, `DerivedColumn`, `SelectAlias`), `Name`, `Schema`, `Table` (for a column), `DefinitionInScript` (`TextRange?`: the alias declaration, the CTE name, the derived column in the inner select list), `DefinitionInSchema` (`bool`: the object exists in the snapshot).
- Resolution rules: an alias resolves to its `FROM` item; a qualified column `a.x` to the source of `a`, then to the column of the table or to the derived column of the sub-query / CTE (following the inner select list, one level per hop, through nested CTEs); an unqualified column to the single visible source that has it (ambiguous -> `null`, no guess); identifiers compared with the dialect's case folding (P19-01 quoting rules).
- Usages are limited to the statement for aliases and derived names, and to the whole script for tables, views and their columns.
- Pure, no UI; performance: a 5,000-line script resolves in under 50 ms (measured in a test with a stopwatch bound, like the completion guard).

## Out of scope
- Editor commands (P21-23), cross-file usages (P21-27 does rename across files).

## Tasks
- [ ] Table-driven tests per dialect: alias, CTE (also recursive), sub-query in `FROM`, correlated sub-query, `JOIN ... USING`, select alias in `ORDER BY`, quoted mixed-case names, ambiguous column, unknown table.
- [ ] Implement over the existing parser and walker (make `SqlTreeWalker` reusable, no second walker).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- The parser is error tolerant: a half-typed statement must give `null`, never throw (fuzz test over the corpus truncated at every 50th character).

## Open points (defaults)
- None.

## Suggested commit message
Resolve the symbol under the caret and its usages in a SQL script
