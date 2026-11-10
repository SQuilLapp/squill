# P14-11 - Sql: a column shared by two tables is inserted qualified

Status: done
Depends on: P14-01, P14-04

## Goal
Accepting `id` when two visible tables have an `id` column writes `o.id` (or `orders.id`), never the bare name the server rejects as ambiguous.

## Design decisions (final)
- In `SchemaCompletionProvider.Columns`, when no qualifier was typed and the same column name (case-insensitive) exists in two or more visible sources, **each** such item gets `InsertText` = `quote(alias-or-table).quote(column)`; `Label` stays the bare column name; `FilterText` the bare name; `Detail` keeps today's `owner.type` text (so the rows stay distinguishable); `Documentation` null here. A column that exists in only one source stays bare. With a qualifier typed (`o.`) nothing changes.
- Sources considered: `scope.Sources.Take(scope.LocalCount)` (the innermost statement; `LocalCount` is added by P14-04). A column of an enclosing statement is never qualified or counted. A derived table / CTE column counts like a real column.
- A source without alias and without name (an anonymous derived table) cannot qualify: its columns stay bare.
- `UPDATE t SET |`, `INSERT INTO t (|` and single-source statements never qualify (one source). `SELECT` list, `WHERE`, `ON`, `GROUP BY`, `HAVING`, `ORDER BY` do.

## Out of scope
- Rewriting an already typed bare column, `*` expansion.

## Tasks
- [x] Tests first (`tests/Squill.Sql.Tests/Completion/QualifiedColumnTests.cs`): `select | from users u join orders o on ...` gives `u.id` and `o.id` with details `u.int`, `o.int` and bare `name`, `user_id`; no alias uses the table name; single table stays bare; qualifier typed stays bare; quoting of a qualifier needing quotes per dialect; CTE column shared with a table; derived table without alias stays bare; subquery sources do not qualify against the outer statement.
- [x] Update existing tests whose expectation was the bare ambiguous column, reason in the commit message.
- [x] Implement, XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Insert ambiguous column names qualified with their table or alias
