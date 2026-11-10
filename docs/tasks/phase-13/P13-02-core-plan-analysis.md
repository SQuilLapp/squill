# P13-02 - Core: plan analysis (relative cost, severity, warnings)

Status: todo
Depends on: P13-01

## Goal
Pure functions that turn a `QueryPlan` into what the screen shows: exclusive cost and time per node, relative percentage, severity (normal / alert / critical) and the generic warnings, identical for every engine and testable without a database.

## Design decisions (final)
- `PlanAnalysis.Analyze(QueryPlan plan)` returns `PlanAnalysisResult(IReadOnlyDictionary<int, PlanNodeStats> Nodes, IReadOnlyList<PlanWarning> PlanWarnings, int CriticalCount, int AlertCount, int WarningCount)` (records, `public`).
- `PlanNodeStats(double? ExclusiveCost, double? ExclusiveTimeMs, double? RelativeShare, PlanSeverity Severity, double? MisestimateFactor, IReadOnlyList<PlanWarning> Warnings)`; `PlanSeverity { Normal, Alert, Critical }`.
- Exclusive = node inclusive minus the sum of its children's inclusive, clamped to >= 0 (helper children count like any other). `RelativeShare` = exclusive / root inclusive, using **time when the root has `ActualTimeMs`, otherwise cost**, `null` when neither exists (SQLite) or the root figure is 0; clamped to [0, 1].
- Severity: `Critical` when share >= 0.60; `Alert` when share >= 0.30 or the node has any warning (its own provider warnings plus the ones below); otherwise `Normal`. Thresholds are constants in `PlanAnalysis` and match the legend of the mockup.
- Generic warnings added here (codes in `PlanMessageCodes`, arguments invariant strings): `plan.misestimate` (`Args = [factor, direction]`, direction `under` or `over`) when `Actual` and both `ActualRows / max(Loops,1)` and `EstimatedRows` are known and `max(a/e, e/a) >= 10` (an estimate or actual of 0 is replaced by 1 before dividing); `plan.fullscan` when `Kind == TableScan`, the estimated rows are known and >= 10 000 and the node has no ancestor `Limit` (rule: unknown estimate => no `plan.fullscan`; SQLite has no estimate, its own `plan.noindex` warning is added by its provider in P13-05). Engine-specific warnings (sort spill, SQL Server warnings) come from the providers inside `PlanNode.Warnings` and are only counted here.
- `MisestimateFactor` is exposed so the graph can print `x590`; `CriticalCount` = nodes with severity `Critical`, `AlertCount` = `Alert`, `WarningCount` = distinct warnings over nodes plus plan-level ones.
- Plan-level warnings: none generated here (providers may fill `QueryPlan.Warnings`); the result carries them through.

## Out of scope
- Text of the messages (Presentation, P13-12), index suggestions.

## Tasks
- [ ] `PlanAnalysis`, result records, `PlanMessageCodes` constants (`plan.misestimate`, `plan.fullscan`, plus the codes the providers use, listed in their cards: `plan.sortspill`, `plan.hashspill`, `plan.missingindex`, `plan.implicitconversion`, `plan.noindex`, `plan.nostats`, `plan.nojoinpredicate`, and the eligibility reasons `plan.reason.empty`, `plan.reason.alreadyexplain`, `plan.reason.notexplainable`, `plan.reason.writes`, `plan.reason.noanalyze` used by P13-03).
- [ ] Tests (`tests/Squill.Core.Tests/Plans/PlanAnalysisTests.cs`, hand-built trees): shares sum to <= 1; time preferred over cost; no timing and no cost gives null shares and severity `Normal` except provider warnings; misestimate boundary (9.9 vs 10 vs 590, under and over, zero values); `plan.fullscan` only above 10 000 and not under a limit; clamping when children exceed the parent; counts.

## Acceptance criteria
- Common acceptance of the recipe, section 5.

## Suggested commit message
Compute relative cost, severity and warnings of a query plan
