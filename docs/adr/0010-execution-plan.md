# ADR 0010 - Execution plan (screen M06)

- Status: accepted
- Date: 2026-11-10

## Context
Phase 13 (cards P13-01 to P13-22, [task index](../tasks/README.md)) adds the query execution plan of mockup M06 to SQLite, PostgreSQL, MySQL and SQL Server in both heads. The shared decisions are in [plan-recipe.md](../tasks/phase-13/plan-recipe.md); the end-to-end evidence and the owner's checklist are in [verification/plan.md](../verification/plan.md).

## Decision
- **Own tab.** A plan opens in its own workspace tab `<query tab> · Plan` (`PlanTabViewModel`, collection `WorkspaceViewModel.Plans`, like the ER diagram tabs). One plan tab per query tab; running again replaces its content; closing the query tab closes it. A plan is not a run: no history entry, the results of the query tab stay.
- **`Expliquer` never executes the statement; `Analyser` executes read-only statements only.** `PlanEligibility` (`Squill.Sql/Plans`) refuses `Analyser` for anything that is not `SELECT` / `VALUES` / `TABLE` / `WITH` without `INTO`, `DELETE`, `MERGE`, `UPDATE` or `INSERT`; the refusal reason is the tooltip. On a Production-environment connection `Analyser` asks for a confirmation first.
- **Per-engine mechanisms.** SQLite `EXPLAIN QUERY PLAN` (structure only, no actual plan). PostgreSQL `EXPLAIN (FORMAT JSON)`; actual: `BEGIN READ ONLY; EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON); ROLLBACK` (a volatile function that writes fails instead of writing; when `BEGIN` raises `25001` the user's own transaction is left alone and no `ROLLBACK` is sent). MySQL 8.4 `EXPLAIN FORMAT=JSON`; actual `EXPLAIN ANALYZE` (text tree, no wrapper: a `START TRANSACTION` would commit a transaction the user opened). SQL Server `SET SHOWPLAN_XML ON` / statement / `OFF`; actual `SET STATISTICS XML ON` inside a rolled-back transaction. `QueryPlan.RolledBack` is true only when a wrapper really rolled back; the status bar says so.
- **`IPlanReader` is an optional interface** (`Squill.Data`) implemented by the four connections; `ConnectionSession.PlanSupport` tells the UI what the engine offers (Oracle implements nothing, the buttons are disabled with a reason). Provider code is `<Driver>PlanReader` (runs statements) and `<Driver>PlanParser` (pure text to `QueryPlan`); both internal.
- **Parsers are pure and AOT-safe**: `System.Text.Json` DOM / reader and `System.Xml.Linq` only, no serializer, no reflection. Unknown keys go to `PlanNode.Properties`; a document that cannot be understood raises `PlanParseException` and the tab shows the raw text in `Texte` with the message.
- **Relative cost is computed in Core** (`PlanAnalysis`), per engine rules, so both heads and all engines draw the same percentages, warnings and critical counts. The heads draw only; layout (`PlanLayout`) is pure geometry in Presentation.
- **Data availability is per engine and never invented**: SQLite has no cost, estimate or timing (no bars or percentages); MySQL estimated plans have no timing, MySQL actual plans no cost on some nodes; a figure the engine does not give is an en dash or hidden. The index-suggestion card is drawn only for SQL Server, from the engine's own missing-index data.
- **SQL Server showplan state.** `SET SHOWPLAN_XML` is connection state. `OFF` runs in `finally` with `CancellationToken.None`; if it fails the connection is disposed and the error says the session must be reopened. Docker tests prove that a normal statement runs after an explain, after a failed explain and after a cancelled analyze (provider tests and `PlanFlowScenario`).

## Alternatives considered
- **A plan panel inside the query tab.** Rejected: the mockup gives the plan the full width and the same tab model as the ER diagram; the query results stay visible in the query tab.
- **Analyze in a rolled-back transaction on every engine.** Rejected for MySQL (implicit commit of the user's transaction) and kept elsewhere.
- **Computing cost shares in each head.** Rejected: two copies of the arithmetic would drift.

## Consequences
- The same statements, tab flow and layout are checked on all four engines by one scenario (`tests/Shared/PlanFlowScenario.cs`); SQLite runs everywhere, the others in Podman or Docker.
- `Analyser` of a select that calls a volatile function that writes is safe on PostgreSQL (read-only transaction) and on SQL Server (rollback); on MySQL a function cannot be reached by `EXPLAIN ANALYZE` of a select that writes.

## Known limits
- **Parallel plans are approximate.** PostgreSQL `Gather` and SQL Server `Parallelism` are drawn as ordinary nodes; time is the engine's inclusive figure and relative percentages on parallel plans are approximate.
- **8 kB block size.** PostgreSQL buffer counts are converted to bytes with 8192 per block (the default; a server built with another block size is shown wrong).
- **MySQL `EXPLAIN ANALYZE` is parsed from indented text** and depends on the 8.x format (tested against 8.4); an unrecognized line becomes a raw node or `PlanParseException` with the `Texte` view.
- **MariaDB is unsupported** (different JSON and analyze output): the provider says so (`PlanUnavailableException`, reason `engine`).
- **Parameters.** PostgreSQL explains with bound parameters. SQL Server showplan cannot compile a statement with bound parameters: `Expliquer` says so (reason `parameters`, found by the Docker tests); an actual plan with parameters may be unavailable too. MySQL reports `parameters` when the server refuses `?` in `EXPLAIN`. Values are never substituted silently. Positional placeholders are refused as in a normal run.
- **MySQL has no analyze wrapper**: `EXPLAIN ANALYZE` accepts `SELECT` only, which is what `PlanEligibility` allows, but a read-only guarantee comes from the guard alone.
- **SQL Server showplan state risk** is mitigated as above, not removed: if the `OFF` batch cannot run, the session must be reopened.
- **Windows were not driven.** The WinUI and Avalonia plan screens were never compared with the M06 PNGs by an agent session; see [verification/plan.md](../verification/plan.md) and section 13 of [visual-spec.md](../design/visual-spec.md).

## Not done
Plan comparison (`Comparer...`), plan history and saved plans, live statistics, index suggestions for PostgreSQL and MySQL, export of a plan to a file, Oracle, MariaDB, cost of a statement inside a stored routine.
