# P13-05 - Data.Sqlite: EXPLAIN QUERY PLAN

Status: done
Depends on: P13-04, P13-02

## Goal
`Expliquer` works on SQLite: the `EXPLAIN QUERY PLAN` rows become a `QueryPlan` tree. SQLite has **no cost, no row estimate and no timing**: those fields stay `null` and `SupportsActualPlan` is false (recipe, section 2). This card needs no container and its tests are never skipped.

## Design decisions (final)
- `SqliteConnection` implements `IPlanReader`. `ExplainAsync` with `Actual` throws `PlanUnavailableException("engine", ...)`. For `Estimated` it runs `EXPLAIN QUERY PLAN <sql>` on the same connection with the same parameters (through the existing execution path so cancel and interrupt behave like a run) and reads `id`, `parent`, `notused`, `detail` of every row.
- `SqlitePlanParser` (internal, pure: takes the rows, returns `QueryPlan`): builds the tree from `id`/`parent` (parent 0 = child of a synthetic root `Query` node, `Kind = Result`, so there is always a single root; if exactly one top-level row exists use it as root). Maps `detail`:
  - `SCAN <table> [AS alias]` (also `SCAN <table> USING COVERING INDEX x`, `SCAN CONSTANT ROW`): `TableScan` (with `USING ... INDEX` -> `IndexScan`), `Target` = table plus alias, `Detail` = the index text if any.
  - `SEARCH <table> USING [COVERING] INDEX <name> (<cond>)` / `USING INTEGER PRIMARY KEY (rowid=?)`: `IndexSeek`, `Target` = `<table> · <index>`, `Detail` = the parenthesized condition.
  - `USE TEMP B-TREE FOR ORDER BY|GROUP BY|DISTINCT|RIGHT PART OF ORDER BY`: `Sort` (or `Aggregate` for GROUP BY / DISTINCT).
  - `COMPOUND QUERY`, `LEFT-MOST SUBQUERY`, `UNION ALL`/`UNION USING TEMP B-TREE`: `Union`; `CO-ROUTINE`, `MATERIALIZE`, `CORRELATED SCALAR SUBQUERY`, `SCALAR SUBQUERY`, `LIST SUBQUERY`: `Subquery`/`Materialize`; anything else: `Other` with `Operation` = the whole detail text. Never throw on unknown text.
  - `Operation` = the leading words (`SCAN`, `SEARCH`, `USE TEMP B-TREE`, ...), shown as the node name.
- Provider warning `plan.noindex` on every plain `SCAN <table>` node without `INDEX` (not `SCAN CONSTANT ROW`); SQLite cannot know table sizes so the rule is unconditional. The UI words it as "Full table scan", amber, never critical because no share exists.
- `SourceFormat = Text`, `SourceText` = the rows joined as an indented tree (`|--SCAN t` style, the format of the `sqlite3` shell).
- `QueryPlan.ExecutionTimeMs`, `PlanningTimeMs`, `BuffersReadBytes` null; `RolledBack` false.

## Out of scope
- Actual statistics (`sqlite3_stmt_scanstatus` is not exposed by the driver), `EXPLAIN` opcode listing.

## Tasks
- [x] Reader + parser.
- [x] Unit tests of the parser over committed sample rows (`tests/Squill.Data.Sqlite.Tests/Plans/*.txt`, captured by running the real provider once): single table scan, search by index and by rowid, join of two tables (two siblings), order by (temp b-tree), group by, subquery, union, unknown line.
- [x] Real-database tests (no container) on the SQLite sample schema (`tests/Shared/SampleSchema/sqlite.sql`): the join+order statement of P13-11 gives a tree whose leaves name the real tables; parameters bound; `Actual` throws `PlanUnavailableException`; a normal statement runs afterwards.

## Acceptance criteria
- Common acceptance of the recipe, section 5; AOT probe `tools/Squill.AotProbe.Sqlite` publishes with zero IL2xxx/IL3xxx.

## Suggested commit message
Read the SQLite query plan
