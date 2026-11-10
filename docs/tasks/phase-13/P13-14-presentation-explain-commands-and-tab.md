# P13-14 - Presentation: Expliquer / Analyser commands and the plan tab

Status: todo
Depends on: P13-12, P13-13, P13-03, P13-04

## Goal
The query tab can produce a plan of the statement under the caret; the workspace holds one plan tab per query tab; errors, unsupported cases and production safety are handled. Look at how `DiagramTabViewModel` is owned by `WorkspaceViewModel` (`Diagrams`, `ActiveDiagram`, `OpenDiagram`, `CloseDiagram`) and at `QueryTabViewModel.ExecuteAsync` (cancel, `IsRunning`, history) and copy the patterns.

## Design decisions (final)
- `ConnectionSession` gets `ValueTask<QueryPlan> ExplainAsync(string statement, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken ct)`: takes the same connection gate as a normal run (a plan never runs concurrently with a statement on the same connection), `connection as IPlanReader` else `PlanUnavailableException("engine")`. `ConnectionSession.PlanSupport` exposes `CanExplain` (reader present) and `CanAnalyze` (`SupportsActualPlan`).
- `QueryTabViewModel` gains `ExplainCommand` (shortcut `Ctrl+E`) and `AnalyzeCommand` (shortcut `Ctrl+Maj+E`, the one printed on `Relancer` in the mockup); the shortcut texts are constants the heads read; check no existing binding already uses them and stop with a proposed fix if one does. Both: resolve the statement with `RunTarget.Resolve`, evaluate `PlanEligibility`; when not allowed, set `StatusText` to the reason text (key `plan.reason.<code>`) and do nothing; when parameters are needed, reuse the parameter prompt flow of `P4-08` (values remembered per file) before running; set `IsRunning` like a run (Cancel button works); run `ExplainAsync`, build `PlanViewModel`, raise `PlanReady(PlanTabViewModel)`; `CanExplain` / `CanAnalyze` observable properties (false while `IsRunning`, when the session has no reader, or the eligibility fails) with `ExplainDisabledReason` / `AnalyzeDisabledReason` texts for tooltips.
- **Production safety** (decision, flagged to the owner in the card report): `Analyser` on a profile whose `ConnectionEnvironment` is `Production` asks `IDialogService.ConfirmAsync` first (`plan.analyze.confirm.title/message`: the statement is executed, in a rolled-back transaction where the engine allows it). No confirmation for the other environments or for `Expliquer`.
- Errors: `PlanUnavailableException` -> status line with the reason (`plan.unavailable.<reason>`); `PlanParseException` -> plan tab opens in `Text` mode showing `SourceText` and a message line; any other exception (SQL error of the server, timeout, cancel) -> the same error presentation as a failed run in the query tab (reuse; cancel shows `Cancelled`). A plan never writes a history entry (it is not a run); document it.
- `PlanTabViewModel : ViewModelBase` (`Source` = the `QueryTabViewModel`, `Title` = `<Source.Title> · Plan` kept in sync, `EnvironmentColor` from the source profile, `Plan` (`PlanViewModel`), `Statement`, `Mode`, `IsBusy`, `Rerun` command (`Relancer`: same statement and mode as the last plan, from the **stored statement** not the editor's current text; refreshes `Plan` in place), `Close`).
- `WorkspaceViewModel`: `Plans` collection, `ActivePlan`, `OpenPlan(PlanTabViewModel)` (reuse an existing plan tab of the same query tab and replace its content), `ClosePlanCommand`, removal when the query tab or its connection closes (same as diagrams, line ~186), only one of query tab / diagram / plan is active at a time (copy how `ActiveDiagram` clears the others). Status bar text source: `ActivePlan.Plan.StatusLine`, right side `NodeCountText`, `ExecutionTimeMs`, server version.

## Out of scope
- Any drawing (heads), plan comparison, saving a plan.

## Tasks
- [ ] Session method, command and state on `QueryTabViewModel`, `PlanTabViewModel`, workspace integration, strings fr/en (`plan.reason.*`, `plan.unavailable.*`, `plan.analyze.confirm.*`, `tab.plan`, button labels `Expliquer`, `Analyser`, `Relancer`, `Arbre`, `Graphique`, `Texte`, column titles).
- [ ] Tests with the fake provider of P13-04: statement under caret is the one passed; write statement refused for analyze with the reason text; SQLite-like fake disables analyze; production asks confirmation (declined -> nothing called, accepted -> called once); parse failure opens Text mode; closing the query tab closes its plan tab; re-run uses the stored statement even after the editor changed; explain while a run is in progress is disabled; cancel mid-explain; no history entry written.

## Acceptance criteria
- Common acceptance of the recipe, section 5; localization guards green.

## Suggested commit message
Run Expliquer and Analyser from the query tab and open plan tabs
