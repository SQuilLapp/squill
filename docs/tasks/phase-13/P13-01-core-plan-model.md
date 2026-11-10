# P13-01 - Core: engine-neutral query plan model

Status: done
Depends on: none

## Goal
The immutable records that every engine's plan is translated into, so Presentation and the heads never see JSON, XML or engine names. Read [plan-recipe.md](plan-recipe.md) first.

## Design decisions (final)
Namespace `Squill.Core.Plans`, folder `src/Squill.Core/Plans/`, all `public sealed record` / enum with XML docs:
- `PlanMode { Estimated, Actual }`; `PlanSourceFormat { Text, Json, Xml }`.
- `PlanNodeKind` (drives the icon, never the layout): `TableScan, IndexScan, IndexSeek, Join, Aggregate, Sort, Limit, Filter, Materialize, Helper, Union, Subquery, Parallel, Result, Other`.
- `PlanProperty(string Name, string Value)` (shown in the details panel, already formatted by the provider except numbers, which stay invariant strings).
- `PlanWarning(string Code, IReadOnlyList<string> Args)`; `PlanSuggestion(string Title, string Text)` (SQL Server only: the `CREATE INDEX` text).
- `PlanNode(int Id, string Operation, PlanNodeKind Kind, string? Target, string? Detail, double? EstimatedRows, double? EstimatedCost, double? StartupCost, int? RowWidth, double? ActualRows, double? ActualTimeMs, long? Loops, IReadOnlyList<PlanProperty> Properties, IReadOnlyList<PlanWarning> Warnings, bool IsHelper, IReadOnlyList<PlanNode> Children)`.
  - `Id` is the pre-order index, root = 0, unique per plan.
  - `EstimatedRows` is per execution of the node; `ActualRows` is the **total over all loops**; `Loops` the number of executions; `EstimatedCost` is **cumulative** (the node and everything below it), `ActualTimeMs` is **inclusive and total over loops** (providers convert the engine's per-loop figure). `null` means the engine does not report it.
  - `IsHelper` marks nodes the graph draws as an edge pill instead of a card (PostgreSQL `Hash`).
- `QueryPlan(DatabaseDialect Dialect, PlanMode Mode, string Statement, PlanNode Root, IReadOnlyList<PlanWarning> Warnings, IReadOnlyList<PlanSuggestion> Suggestions, double? PlanningTimeMs, double? ExecutionTimeMs, long? BuffersReadBytes, bool RolledBack, PlanSourceFormat SourceFormat, string SourceText)` with `IEnumerable<PlanNode> Flatten()` (pre-order) and `PlanNode? Find(int id)`.
- A static `PlanNodeBuilder` is **not** added; providers build records directly. A helper `PlanIds.Assign(PlanNode root)` (internal use by providers through `InternalsVisibleTo` is NOT allowed: make it `public static`) renumbers ids pre-order so parsers can build nodes with `Id = 0` and finish with one call.

## Out of scope
- Computation of percentages and warnings (P13-02), any parsing (P13-05 ...).

## Tasks
- [x] Records, enums, `Flatten`, `Find`, `PlanIds.Assign`.
- [x] `tests/Squill.Core.Tests/Plans/PlanModelTests.cs`: `Flatten` order on a 3-level tree, `Find` hit/miss, `Assign` gives 0..n-1 pre-order and does not mutate the input (records are rebuilt).

## Acceptance criteria
- Common acceptance of the recipe, section 5. Core still references nothing new.

## Suggested commit message
Add the engine-neutral query plan model to Core
