# P13-21 - End-to-end verification: execution plan on the four engines

Status: todo
Depends on: P13-11, P13-17, P13-20

## Goal
Prove the whole path (query tab -> session -> provider -> view-model -> layout) on the four engines through the real Presentation classes, and give the owner a checklist for the part nobody can automate (the windows). Procedure and report format: [phase-11/verification-recipe.md](../phase-11/verification-recipe.md) sections 1 to 3; the report is `docs/verification/plan.md` (the owner ticks it, like `docs/verification/release.md`).

## Design decisions (final)
- Automated, `tests/Shared/PlanFlowScenario.cs` (pattern of `QueryFlowScenario`): per engine, through `QueryTabViewModel`, `SessionManager`, the real provider and `WorkspaceViewModel`: `ExplainCommand` on the statement under the caret opens a plan tab with a non-empty `Rows` and a computed layout (no overlap), `AnalyzeCommand` on a select (not SQLite: command disabled with the reason), `AnalyzeCommand` on an `UPDATE` is refused and **the data is unchanged**, analyze of a select that calls a writing function leaves no row (PostgreSQL, SQL Server), a normal `Run` works after each of explain, failed explain and cancelled analyze (SQL Server showplan state), `Relancer` after editing the editor text re-explains the old statement, parameters prompt then explain, closing the query tab closes the plan tab, a syntax error shows the server error and opens no tab. SQLite runs always; the others Docker-gated (`rtk pwsh eng/run-docker-tests.ps1 -Engine all`).
- Manual checklist `docs/verification/plan.md`, one section per engine (`SQLite`, `PostgreSQL`, `MySQL`, `SQL Server`), each step with the exact clicks, both heads (WinUI first, Avalonia smoke), in `SQUILL_LANGUAGE=fr` and once `en`: open the sample-schema join, `Expliquer` (tab, three views, selection sync, zoom, `Relancer`), `Analyser` (figures, pills, misestimate or spill if the sample shows one, status line `transaction annulée`), cost-free SQLite view (nothing invented), SQL Server index suggestion copy, production confirmation on a Production profile, a statement that cannot be analyzed (reason shown), cancel of a long `Analyser`, plan of a 50-table-join generated statement stays usable, Light/Dark. Boxes unchecked, `Result` column blank.
- Bugs found: fix in this commit when under ~30 lines with a regression test, else a new card `P13-23` onwards (added to the index); an engine/parser gap already known is listed in the report, not hidden.

## Out of scope
- Docs and ADR (P13-22).

## Tasks
- [ ] Scenario and its test classes (one per driver test project, WinUI/Avalonia unaffected), `docs/verification/plan.md`, clean-up (`podman ps -a` empty, `dotnet build-server shutdown`).

## Acceptance criteria
- Common acceptance of the recipe, section 5, Docker run for all engines green with nothing skipped, no container left. The card status is `automated done, manual pending` until the owner ticks the checklist.

## Suggested commit message
Verify the execution plan end to end and add the manual checklist
