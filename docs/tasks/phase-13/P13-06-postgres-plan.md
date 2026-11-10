# P13-06 - Data.Postgres: EXPLAIN (FORMAT JSON), estimated and analyze

Status: done
Depends on: P13-04, P13-02, P11-02

## Goal
`Expliquer` and `Analyser` work on PostgreSQL, with costs, estimated and actual rows, timings, buffers, workers and sort/hash spill warnings. Container: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres` (stop and report if Podman is unavailable).

## Design decisions (final)
- `PostgresDatabaseConnection` implements `IPlanReader` (`SupportsActualPlan` true) through `PostgresPlanReader`.
  - Estimated: `EXPLAIN (FORMAT JSON) <sql>` (never executes). Actual: `BEGIN READ ONLY`, `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) <sql>`, then `ROLLBACK` in `finally` (CancellationToken.None). Each as its own `ExecuteAsync` on the same connection. Recipe risks 2, 3 and 4 apply: the SQLSTATE `25001` warning (read through the connection `Notice` event, `PostgresNotice.SqlState`) means a user transaction is open: skip `ROLLBACK`, `RolledBack = false`. A `permission` or syntax error from the server propagates as the normal driver exception (shown like a statement error).
  - Parameters: bind them as the normal run does; if the server refuses `EXPLAIN` with parameters, throw `PlanUnavailableException("parameters", ...)`.
- `PostgresPlanParser` (internal, pure, `JsonDocument`): input `[{"Plan": {...}, "Planning Time": x, "Execution Time": y, "Triggers": [...]}]`, recursion over `Plans`.
  - `Operation` = `Node Type` (+ ` Scan`/`Join` as given); `Kind`: `Seq Scan` -> `TableScan`; `Index Scan`, `Index Only Scan`, `Bitmap Index Scan`, `Bitmap Heap Scan` -> `IndexScan` (`Index Only`/`Bitmap Index`: `IndexSeek` when an `Index Cond` exists); `Nested Loop`, `Hash Join`, `Merge Join` -> `Join`; `Aggregate`, `HashAggregate`, `GroupAggregate`, `WindowAgg` -> `Aggregate`; `Sort`, `Incremental Sort` -> `Sort`; `Limit` -> `Limit`; `Hash` -> `Helper` with `IsHelper = true`; `Materialize`, `Memoize`, `CTE Scan`, `Subquery Scan` -> `Materialize`/`Subquery`; `Append`, `Merge Append`, `SetOp` -> `Union`; `Gather`, `Gather Merge` -> `Parallel`; `Result` -> `Result`; unknown -> `Other`.
  - `Target` = `Relation Name` + alias (`lignes_commande lc`), or `Index Name` joined with ` · `; `Detail` = `Hash Cond` / `Join Filter` / `Merge Cond` / `Sort Key` (joined with `, `, prefixed `ASC`/`DESC` as printed) / `Group Key` / `Index Cond` / `Filter` (first present in that priority; the rest in `Properties`).
  - Numbers: `Startup Cost`, `Total Cost` (cumulative), `Plan Rows` (per execution), `Plan Width`; actual: `Actual Rows` and `Actual Total Time` are **per loop**, convert to totals: `ActualRows = Actual Rows * Actual Loops`, `ActualTimeMs = Actual Total Time * Actual Loops`, `Loops = Actual Loops`. Rows removed (`Rows Removed by Filter` x loops, label `Lignes écartées`), `Workers Planned`/`Workers Launched`, `Sort Method`/`Sort Space Used`/`Sort Space Type`, `Hash Batches`, `Peak Memory Usage`, `Shared Hit/Read/Dirtied/Written Blocks`, `Heap Fetches`, `Parent Relationship`, `Join Type`, `Strategy`, `Output` all become `Properties` (names as PostgreSQL prints them; the UI translates the few it knows).
  - Warnings: `plan.sortspill` (`Args = [method, spaceKb]`) when `Sort Space Type` is `Disk` or `Sort Method` starts with `external`; `plan.hashspill` when `Hash Batches` > 1 on a `Hash` node.
  - Plan level: `PlanningTimeMs`, `ExecutionTimeMs` from the JSON; `BuffersReadBytes` = (sum over nodes of `Shared Read Blocks`) * 8192 (block size assumed 8 kB, documented limit) when `BUFFERS` data exists; `SourceFormat = Json`, `SourceText` = the JSON as received (indented for the `Texte` view).
- Ids assigned with `PlanIds.Assign`.

## Out of scope
- JIT section, triggers section (kept in `Properties` of the root only as text), `auto_explain`, `EXPLAIN (GENERIC_PLAN)`.

## Tasks
- [x] Reader, parser, samples captured from `postgres:17-alpine` under `tests/Squill.Data.Postgres.Tests/Plans/` (estimated join+aggregate+sort+limit; analyze of the same; a bitmap heap scan; a parallel plan with `Gather`; a sort spilled to disk with `set work_mem = '64kB'`).
- [x] Unit tests of the parser (shapes, per-loop conversion, spill warnings, helper node, unknown node type, truncated JSON -> `PlanParseException` with source text).
- [x] Docker-gated tests: estimated and analyze plans of the sample schema statement; analyze of a `select` calling a writing function fails and leaves no row; analyze inside a user `BEGIN` keeps the user's transaction; parameters; the connection runs a normal statement after each case and after a cancel.

## Acceptance criteria
- Common acceptance of the recipe, section 5, Docker run for postgres green, AOT probe `tools/Squill.AotProbe.Postgres` publishes clean.

## Suggested commit message
Read the PostgreSQL query plan, estimated and analyzed
