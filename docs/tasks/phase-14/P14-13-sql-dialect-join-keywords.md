# P14-13 - Sql: join and clause keywords that exist on the engine and make sense at the caret

Status: done
Depends on: P14-09

## Goal
The keyword popup stops proposing words the engine rejects (`FULL JOIN` on MySQL, `USING` on SQL Server, `ON` after `CROSS JOIN t`) and adds the ones it has (`NATURAL JOIN`, `CROSS APPLY`, `JOIN LATERAL`, `STRAIGHT_JOIN`).

## Design decisions (final)
- In `KeywordCompletionProvider` the single `Joins` array becomes a per-dialect list: PostgreSQL `JOIN, INNER JOIN, LEFT JOIN, RIGHT JOIN, FULL JOIN, CROSS JOIN, NATURAL JOIN, JOIN LATERAL, LEFT JOIN LATERAL`; MySQL `JOIN, INNER JOIN, LEFT JOIN, RIGHT JOIN, CROSS JOIN, NATURAL JOIN, STRAIGHT_JOIN` (no `FULL`); SQL Server `JOIN, INNER JOIN, LEFT JOIN, RIGHT JOIN, FULL JOIN, CROSS JOIN, CROSS APPLY, OUTER APPLY` (no `NATURAL`); SQLite `JOIN, INNER JOIN, LEFT JOIN, RIGHT JOIN, FULL JOIN, CROSS JOIN, NATURAL JOIN` (RIGHT / FULL need SQLite 3.39, which the bundled `Microsoft.Data.Sqlite` provides: say so in the XML doc); Oracle the ANSI list plus `CROSS APPLY`, `OUTER APPLY` (12c). No `OUTER` spellings (not needed).
- After a joined table (`ClauseKind.Join when afterSource`): `ON` is offered only when the join that owns the caret is not `CROSS` / `NATURAL` / apply (use `JoinContextAnalyzer.AnalyzeOn` from P14-09); `USING` only on PostgreSQL, MySQL, SQLite, Oracle (not SQL Server); `AS` as today.
- Nothing else in the keyword lists changes (clauses, expression keywords).
- The words are the **upper-case canonical form**; P14-14 applies the case setting.

## Out of scope
- New statements keywords, `MERGE` / `WITH` templates (P14-16), validating the user's own text.

## Tasks
- [x] Tests first (extend `tests/Squill.Sql.Tests/KeywordCompletionTests.cs` or a new `JoinKeywordTests.cs`): per dialect the exact join list after `from a `; `from a cross join b |` has no `ON`; `from a natural join b |` no `ON`; `from a left join b |` has `ON` and (PostgreSQL) `USING`, SQL Server has no `USING`; `from a join b on x = y |` unchanged; `from a, |` unchanged.
- [x] Update the existing tests that listed `FULL JOIN` for MySQL, reason in the commit message.
- [x] Implement, XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Offer the join keywords each engine accepts
