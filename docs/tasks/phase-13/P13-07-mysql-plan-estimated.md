# P13-07 - Data.MySql: EXPLAIN FORMAT=JSON (estimated)

Status: done
Depends on: P13-04, P13-02, P11-03

## Goal
`Expliquer` works on MySQL 8.4 with access type, key, estimated rows and costs. Container: `rtk pwsh eng/run-docker-tests.ps1 -Engine mysql` (stop and report if Podman is unavailable). `Analyser` on MySQL comes in P13-08; until then `SupportsActualPlan` is false.

## Design decisions (final)
- `MySqlDatabaseConnection` implements `IPlanReader` through `MySqlPlanReader`: `EXPLAIN FORMAT=JSON <sql>` returns one row, one column holding the JSON text. MariaDB servers (version string contains `MariaDB`) throw `PlanUnavailableException("engine", ...)`.
- `MySqlPlanParser` (internal, pure, `JsonDocument`) maps the **heterogeneous** `query_block` structure to a tree. Rules (unknown keys never throw; they go to `Properties` of the nearest node):
  - `query_block` is the root `Query` node (`Kind = Result`) with `EstimatedCost = cost_info.query_cost`; the plan is one root even when the block has a single operation (then root has one child).
  - Wrapper keys become one node each, nesting in this order when present: `ordering_operation` -> `Sort` (`using_filesort`), `grouping_operation` -> `Aggregate`, `duplicates_removal` -> `Aggregate` (`Distinct`), `windowing`, then the content below.
  - `nested_loop` (array of `{table: ...}`) becomes a left-deep chain of `Nested Loop` `Join` nodes: for tables t1,t2,t3 -> `Join(Join(t1,t2),t3)`; with one table, no join node. Join cost: the `prefix_cost` of the last table inside.
  - `table` -> a scan node: `access_type` `ALL` -> `TableScan`; `index` -> `IndexScan`; `ref`, `eq_ref`, `const`, `system`, `range`, `ref_or_null`, `fulltext`, `index_merge` -> `IndexSeek`; `Operation` = `Table scan` / `Index scan` / `Index lookup` / `Index range scan` / `Single-row lookup` (these English words are fixed data, the UI shows them as-is). `Target` = `table_name` + ` · ` + `key` when a key is used; `Detail` = `attached_condition` or `used_key_parts` joined; `EstimatedRows` = `rows_examined_per_scan`, `EstimatedCost` = `cost_info.prefix_cost` (cumulative along the join), `RowWidth` null, other fields (`filtered`, `rows_produced_per_join`, `possible_keys`, `using_index`, `used_columns`, `read_cost`, `eval_cost`, `data_read_per_join`) -> `Properties`.
  - `union_result` -> `Union` with `query_specifications` as children; `subqueries`, `attached_subqueries`, `materialized_from_subquery` -> `Subquery` nodes whose child is their own `query_block`.
  - Numeric strings (`"12.50"`) are parsed with `CultureInfo.InvariantCulture`; unparsable -> `null`.
- Warnings: `plan.fullscan` is **not** set here (P13-02 rule handles estimated rows >= 10 000); `plan.noindex` on `ALL` access with `rows_examined_per_scan` >= 10 000 is already covered by `plan.fullscan`, do not duplicate; `using_temporary_table`/`using_filesort` become `Properties` only.
- `SourceFormat = Json`, `SourceText` = the JSON as received. Warnings text rows of `SHOW WARNINGS` are ignored.

## Out of scope
- `EXPLAIN ANALYZE` (P13-08), optimizer trace, MariaDB.

## Tasks
- [x] Reader, parser, samples from `mysql:8.4` under `tests/Squill.Data.MySql.Tests/Plans/` (single table, join of two and three tables, order by + group by, union, subquery in where, derived table).
- [x] Unit tests of the parser (left-deep join chain, wrapper nesting, numeric strings, unknown key survives, truncated JSON -> `PlanParseException`).
- [x] Docker-gated tests: plan of the sample-schema join statement; parameters (`?` / `@p` as the driver binds them: if `EXPLAIN` rejects bound parameters throw `PlanUnavailableException("parameters")`); normal statement afterwards.

## Acceptance criteria
- Common acceptance of the recipe, section 5, Docker run for mysql green, AOT probe `tools/Squill.AotProbe.MySql` clean.

## Suggested commit message
Read the MySQL query plan (estimated)
