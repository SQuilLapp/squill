# P13-09 - Data.SqlServer: SHOWPLAN_XML (estimated)

Status: done
Depends on: P13-04, P13-02, P11-04

## Goal
`Expliquer` works on SQL Server: estimated plan from `SET SHOWPLAN_XML ON`, with costs, estimated rows, engine warnings and the engine's own missing-index suggestion. Container: `rtk pwsh eng/run-docker-tests.ps1 -Engine sqlserver` (4 GiB Podman machine; stop and report if unavailable). Actual plans come in P13-10 (`SupportsActualPlan` false until then).

## Design decisions (final)
- `SqlServerDatabaseConnection` implements `IPlanReader` through `SqlServerPlanReader`. Sequence on the single connection, **each its own batch/`ExecuteAsync`**: `SET SHOWPLAN_XML ON` -> the statement (returns one result set, one row, one XML column) -> `SET SHOWPLAN_XML OFF` in `finally` with `CancellationToken.None`. Recipe risk 1 is the heart of this card: if `OFF` fails or the connection is cancelled mid-way, dispose the connection (so the pool never hands out a connection stuck in showplan mode: also `SqlConnection.ClearPool`) and throw `PlanUnavailableException("engine", "session must be reopened")`. Parameters: `sp_executesql` through SqlClient with showplan on compiles without executing; prove it in the Docker test, otherwise `PlanUnavailableException("parameters")`.
- `SqlServerPlanParser` (internal, pure, `XDocument`, namespace `http://schemas.microsoft.com/sqlserver/2004/07/showplan`): one `StmtSimple` (first; others ignored with a root property `Other statements: n`). Root `RelOp` is the plan root; children of a `RelOp` are the `RelOp` descendants whose nearest `RelOp` ancestor is it (operator-specific wrapper elements such as `NestedLoops`, `Hash`, `Sort` sit in between).
  - `Operation` = `PhysicalOp` (`Clustered Index Scan`, `Index Seek`, `Hash Match`, `Nested Loops`, `Sort`, `Stream Aggregate`, `Top`, `Compute Scalar`, `Parallelism`, ...); `Kind`: `*Scan` (table/clustered/non-clustered/columnstore) -> `TableScan` for `Table Scan` and `Clustered Index Scan`, `IndexScan` for `Index Scan`; `Index Seek`, `Clustered Index Seek`, `RID Lookup`, `Key Lookup` -> `IndexSeek`; `Nested Loops`, `Hash Match` with `LogicalOp` containing `Join`, `Merge Join` -> `Join`; `Hash Match`/`Stream Aggregate` with `Aggregate` -> `Aggregate`; `Sort`, `Top N Sort` -> `Sort`; `Top` -> `Limit`; `Filter` -> `Filter`; `Table Spool`, `Index Spool`, `Row Count Spool` -> `Materialize`; `Concatenation`, `Merge Interval` -> `Union`; `Parallelism` -> `Parallel`; `Compute Scalar`, `Segment`, `Sequence Project` -> `Other`.
  - `Target` = the first `Object` element: `[Schema].[Table]` + `Alias` and ` · ` + `Index`; `Detail` = the `SeekPredicate`/`Predicate`/`Hash Keys`/`OrderBy` text when present (first `ScalarOperator@ScalarString` found, joined ` AND `).
  - `EstimatedRows` = `EstimateRows`, `EstimatedCost` = `EstimatedTotalSubtreeCost` (cumulative), `StartupCost` = null, `RowWidth` = `AvgRowSize`; `Properties`: `EstimateIO`, `EstimateCPU`, `EstimateRebinds`, `EstimateRewinds`, `Parallel`, `Ordered`, `TableCardinality`, `EstimatedExecutionMode`.
  - Warnings: per-`RelOp` `Warnings` element: `SpillToTempDb` -> `plan.sortspill`/`plan.hashspill` (by physical op), `ColumnsWithNoStatistics` -> `plan.nostats`, `PlanAffectingConvert` -> `plan.implicitconversion` (args: the expression), `NoJoinPredicate` -> `plan.nojoinpredicate`; plan-level `StmtSimple/QueryPlan/Warnings` and `MissingIndexes`: each `MissingIndex` becomes a `PlanSuggestion("Missing index", "CREATE INDEX [IX_<table>_<cols>] ON <table> (<equality cols>, <inequality cols>) INCLUDE (<include cols>);")` (column order: `EQUALITY`, `INEQUALITY`, then `INCLUDE` groups of the `ColumnGroup` elements; the generated name is `IX_<table>_<first cols>` limited to 128 chars, identifiers quoted with `[ ]`) and a plan-level `plan.missingindex` warning with the impact percent (`Impact` attribute) as argument. Add the new warning codes to `PlanMessageCodes` (extension of P13-02; adding constants is allowed here).
  - `SourceFormat = Xml`, `SourceText` = the XML (indented). `PlanningTimeMs` from `CompileTime`, `ExecutionTimeMs` null.

## Out of scope
- Actual plans (P13-10), query hints, plan guides, `STATISTICS PROFILE`.

## Tasks
- [x] Reader, parser, samples from the 2022 image under `tests/Squill.Data.SqlServer.Tests/Plans/` (single table, join + aggregate + sort + top, a seek with key lookup, a statement with a missing-index suggestion, a spill/convert warning if reproducible, else hand-edit one and say so).
- [x] Unit tests (nesting through wrapper elements, kind mapping, suggestion text, quoting, warnings, malformed XML -> `PlanParseException`).
- [x] Docker-gated: plan of the sample-schema join; **normal statement runs after a successful explain, after an explain of invalid SQL, and after a cancelled explain**; parameters.

## Acceptance criteria
- Common acceptance of the recipe, section 5, Docker run for sqlserver green, AOT probe `tools/Squill.AotProbe.SqlServer` clean.

## Suggested commit message
Read the SQL Server estimated plan

## Notes from the implementation
- Bound parameters: SqlClient binds them through `sp_executesql`; under `SHOWPLAN_XML ON` the server returns no operator tree for that RPC (and a bare `EXEC sp_executesql` batch yields one `EXECUTE PROC` statement without `RelOp`). The reader therefore throws `PlanUnavailableException("parameters")` when parameters are present and the answer has no `RelOp`; proven by a Docker test.
- The spill and `NoJoinPredicate` warnings are covered by hand-edited samples (not reproducible on a tiny dataset); the other samples are real captures from `mssql/server:2022-latest`.
