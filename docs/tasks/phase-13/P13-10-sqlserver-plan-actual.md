# P13-10 - Data.SqlServer: STATISTICS XML (actual)

Status: done
Depends on: P13-09

## Goal
`Analyser` works on SQL Server: the same XML as P13-09 with runtime counters (actual rows, executions, elapsed time).

## Design decisions (final)
- `SqlServerPlanReader` for `Actual`: `BEGIN TRANSACTION` -> `SET STATISTICS XML ON` -> the statement (**executes**; its own result sets are drained and discarded, then the plan XML result set is read: it is the last result set, column header `Microsoft SQL Server 2005 XML Showplan`) -> `SET STATISTICS XML OFF` -> `ROLLBACK TRANSACTION`, the last two in `finally` with `CancellationToken.None`, same dispose-on-failure rule as P13-09 (recipe risk 1) and `RolledBack = true` when the rollback succeeded. Only statements allowed by `PlanEligibility.CanAnalyze`.
- `SqlServerPlanParser` extension: `RunTimeInformation/RunTimeCountersPerThread` elements per `RelOp`: `ActualRows` = sum over threads of `ActualRows`, `Loops` = sum of `ActualExecutions`, `ActualTimeMs` = max over threads of `ActualElapsedms` (threads run in parallel; documented approximation, summing would overstate), further per-thread values `ActualCPUms`, `ActualLogicalReads`, `ActualPhysicalReads`, `ActualRebinds`, `ActualRewinds` summed into `Properties`. Note SQL Server reports `ActualElapsedms` **inclusive** of children, which matches the model; it is `0` for operators that did not run. `BuffersReadBytes` = (sum of `ActualLogicalReads`) * 8192. `ExecutionTimeMs` = `QueryTimeStats@ElapsedTime` when present else the root's elapsed. `Mode = Actual`.
- Reader sets `SupportsActualPlan = true`.

## Out of scope
- Live query statistics (`sys.dm_exec_query_statistics_xml`), statistics IO/TIME text.

## Tasks
- [x] Reader mode, parser counters, actual samples committed next to the estimated ones.
- [x] Unit tests (multi-thread aggregation, never-executed operator, missing runtime element leaves actual fields null).
- [x] Docker-gated: analyze of a read-only join gives actual rows on every scan and `RolledBack`; a `WITH x AS (DELETE ...)`-like statement is refused by the guard; connection reusable after success, failure and cancel (recipe risk 1 again, with `STATISTICS XML`); a non-read-only function called from a select cannot leave data behind (rolled back: assert row count unchanged).

## Acceptance criteria
- Common acceptance of the recipe, section 5, Docker run for sqlserver green.

## Suggested commit message
Read the SQL Server actual plan

## Notes from the implementation
- When a user transaction is already open (`@@TRANCOUNT > 0`) the reader uses `SAVE TRANSACTION` / `ROLLBACK TRANSACTION <savepoint>` instead of `BEGIN` / `ROLLBACK`, so only the analyzed statement's effects are undone (a Docker test proves it). `RolledBack` is true in both cases.
- The multi-thread aggregation sample is the single-table sample with a second `RunTimeCountersPerThread` element built in the test; the other actual samples are real captures.
