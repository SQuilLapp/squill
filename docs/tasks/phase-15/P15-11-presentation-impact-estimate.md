# P15-11 - Presentation: estimated impact of a modifying statement

Status: done
Depends on: P15-01

## Goal
The production dialog can say "elle supprimera environ 1 248 310 lignes (estimation du planificateur)" when the engine can tell cheaply, and says nothing when it cannot. Recipe: section 5 (rules), section 1 (plan reader facts), risk 7; read `ConnectionSession.ExplainAsync`, `PlanNode`, `PostgresPlanParser`, `MySqlPlanParser`, `SqlServerPlanParser` to see which node carries `EstimatedRows` for a `DELETE` / `UPDATE` / `INSERT ... SELECT`.

## Design decisions (final)
- `src/Squill.Presentation/Safety/ImpactEstimate.cs` (`public sealed record ImpactEstimate(long Rows)`) and `ImpactEstimator` (`public static`): `ValueTask<ImpactEstimate?> EstimateAsync(ConnectionSession session, string statement, IReadOnlyList<QueryParameter> parameters, CancellationToken ct)`.
- Conditions to try: `StatementClassifier` kind is `Insert` (only `INSERT ... SELECT`, i.e. the statement has a `SELECT` after the head), `Update`, `Delete`; `PlanEligibility.Evaluate(...).CanExplain`; `session.PlanSupport.CanExplain`; `session.Transaction.State != Failed`; the dialect is not SQLite. Otherwise return `null` without calling the engine.
- The call is `session.ExplainAsync(statement, parameters, PlanMode.Estimated, linkedCts.Token)` with a **3 s** cap (`CancelAfter`); a timeout, `PlanUnavailableException`, `PlanParseException` or any other exception returns `null` (never throws, never shows an error); cancellation by the caller propagates.
- The figure: walk down from `QueryPlan.Root` skipping data-modifying wrapper nodes (PostgreSQL `ModifyTable` kinds `Insert` / `Update` / `Delete` / `Merge`, SQL Server `Clustered Index Delete|Update|Insert`, `Table Delete|Update|Insert`, `Index Delete|Update`, MySQL none) to the **first node whose `EstimatedRows` is not null**, take `Math.Round` as `long` (at least 0). If the walk finds none, return `null`. If the plan model does not keep the wrapper distinction, the implementer reads how each parser maps its operator names (`PlanNodeKind`) and writes the skip rule in one `static` method with a comment-free name list; the rule is proven by the container tests below, not guessed.
- The estimator does not format; the dialog code formats with `UiText.Culture`.

## Out of scope
- The dialog and the decision to ask (P15-12), caching estimates, estimates for DDL, `INSERT ... VALUES` (no figure).

## Tasks
- [x] `ImpactEstimate`, `ImpactEstimator`.
- [x] Unit tests with a fake `IPlanReader` connection: each kind returns / skips as above, SQLite and `Failed` state return null without a call, a throwing reader returns null, a reader slower than 3 s (fake clock or short injected cap through an `internal` overload taking the `TimeSpan`) returns null, the caller's cancellation propagates, a root with rows 0 and a child with 1000 returns 1000, nothing found returns null.
- [x] Docker-gated, `tests/Shared/ImpactEstimateScenario.cs` per server engine (PostgreSQL, MySQL, SQL Server; SQLite asserts null): table of 1000 rows with statistics up to date (`ANALYZE`; SQL Server `UPDATE STATISTICS`), then `DELETE` without `WHERE`, `UPDATE ... WHERE <range>`, `INSERT ... SELECT`, each estimate within a factor of 2 of the real count (run the statement in a transaction and roll back to get the real count), and the table unchanged afterwards (estimation never executes). Record the observed figures in the card report. MySQL: if `EXPLAIN FORMAT=JSON DELETE` carries no usable rows figure the engine returns `null` and the test asserts that, with a note in the report and in the ADR.

## Acceptance criteria
- Common acceptance of the recipe, section 9 (Docker run for all engines green, nothing skipped).

## Suggested commit message
Estimate the rows a modifying statement will touch from the planner

## Observed figures (containers, 1000 rows, statistics fresh)
- PostgreSQL 17: DELETE 1000 / real 1000; UPDATE ... WHERE 500 / 500; INSERT ... SELECT 1000 / 1000.
- SQL Server 2022: same as PostgreSQL.
- MySQL 8.4: DELETE 1000 / 1000; UPDATE ... WHERE (no index) 1000 / 500; INSERT ... SELECT gives no figure (null), asserted by the scenario.
- SQLite: always null.
