# P14-04 - Sql: join context at the caret (kind, left sources, existing condition)

Status: todo
Depends on: none

## Goal
One pure function says, for a caret in a table position, whether it is a join, which kind, which tables are already in the same FROM list, and whether the join already has a condition.

## Design decisions (final)
- `internal sealed record JoinContext(bool IsJoin, JoinKind Kind, bool IsNatural, bool IsApply, bool HasCondition, IReadOnlyList<VisibleSource> LeftSources)` and `internal static class JoinContextAnalyzer` with `JoinContext Analyze(SqlRequest request, int wordStart, int wordEnd)` in `src/Squill.Sql/Completion/`. `wordStart..wordEnd` is the range of the identifier being typed (the provider already computes it as `range`).
- **Kind** from the significant tokens (dialect tokenizer, never regex) before `wordStart`: the token before the word must be `JOIN` (then `IsJoin`) or `,` inside a FROM list (`Kind = Comma`, `IsJoin` true but no condition ever); words before `JOIN` give the kind: `INNER` -> Inner, `LEFT [OUTER]` -> Left, `RIGHT [OUTER]` -> Right, `FULL [OUTER]` -> Full, `CROSS` -> Cross, none -> Inner; `NATURAL` before the kind sets `IsNatural`; `APPLY` (T-SQL `CROSS APPLY` / `OUTER APPLY`) sets `IsApply`, kind Cross / Left. `LATERAL` before the word is skipped.
- **HasCondition**: the first significant token after `wordEnd` is `ON` or `USING`.
- **LeftSources**: `SqlScopeResolver.Resolve` on the text **with the typed word removed** (`text[..wordStart] + text[wordEnd..]`, caret at `wordStart`), because a half-typed `ord` would otherwise parse as a table called `ord`. Keep only the sources of the innermost statement: extend `ScopeInfo` with `public int LocalCount { get; init; }` (sources of the innermost statement come first in `Sources`; default `0` is wrong for old callers, so the resolver always sets it; grep `new ScopeInfo` first: today only `SqlScopeResolver` builds it) and take `Sources.Take(LocalCount)`. Derived tables and CTEs stay in the list (they have `DerivedColumns`; later cards skip them for foreign keys).
- Returns `IsJoin = false` (and empty sources) for every other position (`FROM x` first table, `INTO`, `UPDATE`, outside a statement).

## Out of scope
- Using the context (P14-07, P14-08), `USING`, `JOIN` inside `MERGE`, `DELETE ... USING`.

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/JoinContextTests.cs`, marked text with `|` like `SchemaCompletionTests`, plus a `ScopeResolverTests` case for `LocalCount`): `from a join |`, `from a inner join |`, `left join`, `left outer join`, `right join`, `full outer join`, `cross join`, `natural join`, `cross apply` on SqlServer, `from a, |`, a half-typed word `join ord|` (word not a source), `from a join b on x=y join |` (two left sources), a subquery `select (select 1 from c join |) from a` (left source only `c`), a caret right before an existing condition (`from a join | on a.x = b.y` -> `HasCondition`, and `from a join ord| on ...` the same), `from |` first table (`IsJoin` false), `insert into |`, `update |`, empty script, comment between `LEFT` and `JOIN`.
- [ ] Implement, XML docs on the internal type and its public members.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Detect the join kind and left tables at a table position
