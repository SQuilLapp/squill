# P13-08 - Data.MySql: EXPLAIN ANALYZE (actual)

Status: done
Depends on: P13-07

## Goal
`Analyser` works on MySQL 8.4 with actual rows, loops and timings. **Riskiest parser of the phase**: `EXPLAIN ANALYZE` only exists as an indented text tree (`FORMAT=TREE`); there is no JSON. If the stop rule below triggers, the card stops with a report and `SupportsActualPlan` stays false for MySQL (the UI already handles that, SQLite does the same).

## Design decisions (final)
- `MySqlPlanReader` for `Actual`: `EXPLAIN ANALYZE <sql>` (one row, one column, text). No transaction wrapper (recipe risk 2); the eligibility guard (P13-03) already restricts to read-only `SELECT`/`WITH`. Server older than 8.0.18 -> `PlanUnavailableException("version", ...)`.
- `MySqlAnalyzeTextParser` (internal, pure): each line is `<indent>-> <operation text>  (cost=<c> rows=<r>) (actual time=<first>..<last> rows=<n> loops=<l>)`; the indent (4 spaces per level) gives the nesting. Per node: `Operation` = text up to the first `:` or ` on `/` using ` (e.g. `Table scan`, `Index lookup`, `Filter`, `Inner hash join`, `Sort`, `Aggregate`, `Limit`, `Nested loop inner join`, `Covering index scan`), `Target` = the table after `on`, `Detail` = the part after `:` (condition, sort key), `EstimatedCost` = `cost` (this node's own cost as MySQL prints it, treated as cumulative like the others), `EstimatedRows` = `rows`, `ActualRows` = `n * loops`, `ActualTimeMs` = `last * loops`, `Loops` = `l`. `(never executed)` -> actual fields 0 with a property `Never executed`. Kind from keywords: `Table scan` -> `TableScan`, `... index ...`/`lookup`/`range` -> `IndexSeek`/`IndexScan`, `join` -> `Join`, `Sort` -> `Sort`, `Aggregate`/`Group` -> `Aggregate`, `Limit` -> `Limit`, `Filter` -> `Filter`, `Materialize`/`Temporary table` -> `Materialize`, else `Other`.
- The estimated costs and the actual figures come from the same output, so the `Actual` plan is complete from this one statement. `ExecutionTimeMs` = root `last` time. `SourceFormat = Text`, `SourceText` = the raw tree. A line that does not parse becomes an `Other` node keeping its raw text (never throws except for empty output -> `PlanParseException`).
- **Stop rule**: capture the real output of the sample statements first; if the format differs from the grammar above in a way that is not mechanical (for example no indentation), stop and propose a card fix.

## Out of scope
- `EXPLAIN ANALYZE FORMAT=JSON` (MySQL 9.x only, not the pinned image), analyzing DML (MySQL refuses).

## Tasks
- [x] Parser, reader switch, samples (`tests/Squill.Data.MySql.Tests/Plans/analyze-*.txt`: join with filter, sort + limit, aggregate, never-executed branch).
- [x] Unit tests (nesting by indent, loops multiplication, never executed, junk line survives, empty output).
- [x] Docker-gated: analyze of the sample-schema join returns actual rows > 0 on the scans; analyzing an `INSERT` is refused by the guard before reaching the server; normal statement afterwards.

## Acceptance criteria
- Common acceptance of the recipe, section 5, Docker run for mysql green. `MySqlDatabaseConnection.SupportsActualPlan` becomes true.

## Suggested commit message
Read the MySQL EXPLAIN ANALYZE tree
