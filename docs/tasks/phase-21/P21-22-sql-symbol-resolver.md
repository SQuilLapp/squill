# P21-22 - Sql: symbol resolution for go to definition and find usages

Status: done
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
- [x] Table-driven tests per dialect: alias, CTE (also recursive), sub-query in `FROM`, correlated sub-query, `JOIN ... USING`, select alias in `ORDER BY`, quoted mixed-case names, ambiguous column, unknown table.
- [x] Implement over the existing parser and walker (make `SqlTreeWalker` reusable, no second walker).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- The parser is error tolerant: a half-typed statement must give `null`, never throw (fuzz test over the corpus truncated at every 50th character).

## Open points (defaults)
- None.

## Suggested commit message
Resolve the symbol under the caret and its usages in a SQL script

## Done notes
- Premise checked: `SqlScopeResolver`, `SqlTreeWalker` (internal), `VisibleSource` and `SqlHoverProvider` exist as stated; the syntax tree keeps names as strings without positions (aliases, CTE names, select aliases), so declarations are located by tokens inside the node span. "P19-01 quoting rules" are about writing quoted names, there is no dialect case-folding helper and the tree drops the quotes: names are compared ignoring case, as completion, hover and diagnostics already do.
- No second walker. `SqlTreeWalker.References` finds the table or column reference under the caret; `SqlScopeResolver` supplies the sources. Changes there: `VisibleSource` gets internal `Origin` (the `TableRef` / `SubquerySource`) and `Cte`, `SqlScopeResolver.Chain` (the statements around an offset) and `OutputNames` became internal.
- New: `SqlSymbol` / `SqlSymbolKind` and `SqlSymbolResolver.SymbolAt` / `Usages` in `Squill.Sql/Analysis`. `Usages` takes an optional third argument `SchemaSnapshot? schema` (not in the card) so unqualified columns resolve the same way as in `SymbolAt`. Usages re-resolve every identifier of the same name and keep those that resolve to the same symbol, so aliases and derived names stay in their statement (their declaration differs) and tables, views and columns span the script (a missing schema or table on either side matches any).
- Rules beyond the card: with a `FROM` source the snapshot does not describe, a column is attributed to it only when nothing else could own it (qualified, or the only candidate), otherwise `null`; a sub-query or CTE whose select list has a star gives `null` (no star expansion); in `ORDER BY` a select alias wins over a column, elsewhere it is the last resort; a recursive CTE refers to itself when the `RECURSIVE` keyword is present or, for Oracle and SQL Server, when its body is a set operation (`SqlScopeResolver` does not make a CTE visible inside its own body, so the resolver finds it through `Chain`).
- Not covered (returns `null` or is skipped): column names in `INSERT` column lists and `UPDATE ... SET` targets, schema parts of a name, three-part names, quoted names containing a dot.
- Tests: `SymbolResolverTests` (6 dialects, alias, qualified column, table/view/unknown, CTE and its columns, nested CTEs, recursive CTE per dialect, sub-query, correlated sub-query, `JOIN ... USING` ambiguity, select alias in `ORDER BY`, quoted mixed-case names, usages, truncation fuzz at every 5th character for every dialect, 5,000-line script under 50 ms).

- Verification: `Squill.slnx` and `Squill.Windows.slnx` build with 0 warnings; `dotnet test --solution Squill.slnx`: every project green (Sql 2158) except that the Avalonia UI test host stopped answering after 149 passed tests, twice, and had to be ended by hand (known flaky area, no test failed, code unrelated to `Squill.Sql`). No AOT publish (the card asks none, no head code changed).
