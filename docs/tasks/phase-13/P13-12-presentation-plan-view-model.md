# P13-12 - Presentation: plan view-model (rows, details, summary, text)

Status: done
Depends on: P13-02

## Goal
Everything the plan screen shows, as view-model state both heads bind to, with no UI framework and no engine knowledge: summary figures, warning pills, the flat table rows, the selected node details, the raw text. Strings in French and English.

## Design decisions (final)
- `PlanViewModel : ViewModelBase` in `Squill.Presentation/Plans/`, built from a `QueryPlan` (`PlanViewModel.Create(QueryPlan plan)` runs `PlanAnalysis.Analyze` once). Properties:
  - `Mode` of the view (`Tree`, `Graph`, `Text`, observable, default `Graph`; `Tree` and `Graph` both show the table, `Text` shows `SourceText`), `Rows` (`IReadOnlyList<PlanRowViewModel>`, pre-order, one per node **including helpers**), `SelectedRow` (observable, default = the node with the highest `RelativeShare`, else the root), `Details` (`PlanDetailsViewModel` for the selection), `Summary` (list of `PlanFigure(Label, Value)`: total duration (`Durée totale`, from `ExecutionTimeMs`), `Coût` (root `EstimatedCost`), `Tampons lus` (`BuffersReadBytes` formatted `431 Mo`), omitting each figure the plan lacks), `CommandText` (the engine statement family shown after the segmented control: `EXPLAIN (ANALYZE, BUFFERS)` style text built by the provider mode and dialect: PostgreSQL `EXPLAIN (FORMAT JSON)` / `EXPLAIN (ANALYZE, BUFFERS)`, MySQL `EXPLAIN FORMAT=JSON` / `EXPLAIN ANALYZE`, SQL Server `SHOWPLAN_XML` / `STATISTICS XML`, SQLite `EXPLAIN QUERY PLAN`), `CriticalCountText` (`1 nœud critique`, plural through `FormatPlural`), `WarningCountText` (`2 avertissements`), both hidden when 0, `NodeCountText` (`10 nœuds`), `StatusLine` (`EXPLAIN ANALYZE exécuté dans une transaction annulée` when `RolledBack`; the estimated variant `Plan estimé : l'instruction n'a pas été exécutée`; the analyze-without-wrapper variant `EXPLAIN ANALYZE exécuté`), `Zoom` (double, 0.25..2, step 0.1, default 1, `ZoomInCommand`, `ZoomOutCommand`, `ResetZoomCommand`; shown as `100 %`).
- `PlanRowViewModel`: `Node`, `Depth`, `Label` (operation) and `Target` (mono part), cells already formatted with the current culture (P10-07 formatting helpers: thousands separator, `0,4 ms`, `1 182 ms`, `-` for null): `RelativeText` (`64 %`), `RelativeShare` (for the bar), `TotalCost`, `EstimatedRows`, `ActualRows`, `Time`, `Loops`, `Severity`, `MisestimateText` (`x590`), `IsHelper`. Row for the root is expanded; collapsing is not in this card (table shows everything, indented by `Depth`).
- `PlanDetailsViewModel`: title (`Seq Scan sur lignes_commande`: operation + `sur` + target when a target exists), `ShareText`, ordered `Properties` (translated names for the few known keys: `Filtre`, `Lignes écartées`, `Tampons`, `Workers`; other names shown as the engine prints them), the node's warnings as texts, plan-level suggestions (`Suggestion d'index` card, only when `Suggestions` is non-empty, `CopyText` string), `CopySuggestionCommand` through the platform clipboard service if one exists (grep `IPlatformServices`; otherwise the head copies).
- Warning text: `IssueText.Format(code, args, fallback)` with catalog keys `plan.warning.<code>`, for example `plan.misestimate` -> `Estimation {0} fois trop {1}` (`basse` / `haute` selected by the arg), `plan.sortspill` -> `Tri sur disque · {1} Ko`, `plan.hashspill`, `plan.fullscan` -> `Lecture complète de la table`, `plan.noindex`, `plan.missingindex`, `plan.implicitconversion`, `plan.nostats`, `plan.nojoinpredicate`. Add every `plan.*` key to `UiTextCatalogFr` and `UiTextCatalogEn` (parity tests of phase 10 must pass).
- `PlanFigure` values use `Squill.Core` figures only; never engine strings other than node names, targets and property values.

## Out of scope
- Graph geometry (P13-13), commands that produce a plan (P13-14), drawing.

## Tasks
- [x] View-models, formatting, catalog keys fr/en.
- [x] Tests (`tests/Squill.Presentation.Tests/Plans/PlanViewModelTests.cs`, hand-built plans for PostgreSQL-shaped, SQLite-shaped (no figures: nothing invented, no pills, `-` cells), SQL Server with suggestion): default selection, summary omissions, plural pills in both languages, status lines, zoom bounds, details title and property translation, row order and depth, rounding of percentages (0.64 -> `64 %`, 0.004 -> `0 %`).

## Acceptance criteria
- Common acceptance of the recipe, section 5; localization guard tests green.

## Suggested commit message
Add the plan view-model and its strings

## Notes from the implementation
- The view's representation is `PlanViewModel.ViewMode` (`PlanViewMode`), not `Mode`, to avoid confusion with `QueryPlan.Mode`.
- A missing figure is an en dash (`–`, as in the recipe), shown by `PlanText.Missing`; the card text said `-`.
- `IPlatformServices` has no clipboard service: the details view-model exposes `CopyText` and the heads copy it.
- Warning texts use literal keys `plan.warning.*` through `PlanText.Warning` (not `IssueText`, whose `issue.` prefix would give `issue.plan.*`); the sort-spill text has a `.size` variant for PostgreSQL, which reports the size. The status line takes the engine statement family as `{0}` (`STATISTICS XML` on SQL Server).
- The engine statement families and a few PostgreSQL property names are listed in `ignored-literals.txt` (not translatable); `plan.prop.workers` joins the same-in-both-languages allow list.
- At most one node can be critical in the time/cost model (exclusive shares sum to at most 1), so the plural of the critical pill is only exercised for 1.
