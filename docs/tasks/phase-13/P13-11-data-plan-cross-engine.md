# P13-11 - Data: one plan scenario run on every engine

Status: todo
Depends on: P13-05, P13-06, P13-08, P13-10

## Goal
A shared scenario (the way `MultiResultScenario` and `CancelScenario` are shared) proves that all four providers give a usable `QueryPlan` for the same statements on the sample schema, so Presentation can rely on the model without engine knowledge. This also pins the "no stuck connection" and "analyze rolls back" guarantees per engine.

## Design decisions (final)
- `tests/Shared/PlanScenario.cs` (compiled into each driver test project the same way the other shared scenarios are; copy the include pattern of `MultiResultScenario`). Statements, written once with per-dialect variants for quoting and limit syntax (use the tables of `tests/Shared/SampleSchema/<engine>.sql`: customers, orders, order_items):
  1. join `orders` to `customers` with a filter, `group by` customer, `order by` total desc, limit 5;
  2. a single-table lookup by primary key;
  3. a subquery (`where id in (select ...)`).
- Assertions (engine-neutral): `Root` not null; ids are 0..n-1 pre-order and unique; the leaf nodes' `Target` texts contain the real table names (case-insensitive) of the statement; `Flatten()` has at least one `Join` and one `Sort` for statement 1 except where the engine folds them (list the exceptions in the scenario as data, not as skipped asserts); estimated plans of PostgreSQL, MySQL, SQL Server have `EstimatedRows` on every scan node and `EstimatedCost` on the root; SQLite has neither; `PlanAnalysis.Analyze` gives shares in [0,1] where costs exist; `Actual` (not SQLite) gives `ActualRows` on every executed scan and `RolledBack` true on PostgreSQL and SQL Server, false on MySQL; after every explain the connection runs `select 1`; cancel during an `Actual` of a slow statement (`pg_sleep`, `WAITFOR DELAY`, `SLEEP`) throws `OperationCanceledException` and the connection still runs `select 1`; an analyze of a write statement never reaches the server (the reader is only called after `PlanEligibility`, so this part is asserted at Presentation level in P13-21; here assert `PlanEligibility` refuses it for the dialect).
- One test class per driver project (`PlanScenarioTests`, Docker-gated except SQLite which always runs) calling the scenario.

## Out of scope
- Presentation, UI.

## Tasks
- [ ] Scenario file, four test classes, any fix of a provider bug the scenario finds (under ~30 lines with a regression test; otherwise a new card `P13-23` onwards, added to the index).

## Acceptance criteria
- Common acceptance of the recipe, section 5; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.

## Suggested commit message
Prove query plans on every engine with a shared scenario
