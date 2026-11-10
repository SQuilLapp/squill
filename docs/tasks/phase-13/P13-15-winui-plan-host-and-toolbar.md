# P13-15 - WinUI head: Expliquer and Analyser buttons, plan tab host, tab strip, status bar

Status: done
Depends on: P13-14, P9-12

## Goal
In the WinUI head the toolbar shows `Expliquer` and `Analyser`, a plan tab opens in the tab strip as `<name> · Plan`, and the plan tab view exists with its toolbar. The three view bodies come in P13-16 (table, details, text) and P13-17 (graph). Look at how the ER diagram tab is hosted: `MainWindow.Diagram.cs`, `MainWindow.xaml(.cs)`, `Views/DiagramTabView.xaml(.cs)`, `Views/TabInfo.cs`, `Views/TabHeader.cs`, `Views/StatusBar.cs`, and the query toolbar in `Views/QueryTabView.cs`.

## Design decisions (final)
- Query toolbar (spec section 5): `Expliquer` secondary button (no hint) between `Script` and `Arrêter`, `Analyser` secondary button with the `Ctrl+Maj+E` kbd chip right after it; tooltips show `ExplainDisabledReason` / `AnalyzeDisabledReason` when disabled; labels through `Loc`/`UiText`.
- Tab strip: plan tabs after the diagram tabs of the same strip, header = 8 px colored square (source environment color) + `ventes.sql` + muted ` · Plan`, close x; active style as the other tabs. Selecting it shows `PlanTabView`; selecting a query tab hides it.
- `Views/PlanTabView.xaml(.cs)` (new): bound to `PlanTabViewModel`; toolbar row (height 40, as the mockup): segmented control `Arbre | Graphique | Texte` bound to `Plan.Mode`, `CommandText` in mono `fg-3`, a 1 px separator, the summary figures (label `fg-2`, value semibold), the two pills (critical = `danger-soft` / `danger`, warnings = `warning-soft` / `warning`, hidden at 0), zoom `-` `100 %` `+`, `Relancer` + `Ctrl+Maj+E` chip (the chip is shown only when the plan in the tab is an `Actual` one; for an estimated plan `Relancer` has no chip and `Ctrl+E` re-runs it). The body is an empty `Grid` placeholder named `BodyHost` that P13-16/17 fill; for this card the `Texte` view is already wired as a read-only monospace `TextBox` (selectable, copyable) so the card is testable.
- Tokens only (`SquillXxxBrush`); if a color of the mockup has no token (amber pill backgrounds exist as `warning-soft`; check `tokens/squill.tokens.json`), stop and propose a token card instead of hard-coding.
- Status bar: when a plan tab is active it shows the plan status line and the right group of the mockup (`<n> nœuds · <ms> ms`, server version) instead of the cursor position.
- Keyboard: `Ctrl+E` and `Ctrl+Maj+E` reach the commands only while a query tab is active; in the plan tab `Ctrl+Maj+E` is `Relancer`.

## Out of scope
- Table, details and graph bodies (P13-16, P13-17), Avalonia.

## Tasks
- [x] Toolbar buttons, `PlanTabView` toolbar and `Texte` body, tab strip and status bar integration, localized labels.
- [ ] Capture (tools of P9-02, section 11 of `visual-spec.md`) of the plan tab toolbar in light and dark, `SQUILL_LANGUAGE=fr`, against `M06-Plan-clair/sombre` (toolbar area only); anything not compared goes to the report as "to check by hand". Not done: the plan tab could not be driven (needs a live connection), so no capture was compared; to check by hand.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI AOT publish with zero IL2xxx/IL3xxx warnings.
- To check by hand: `Expliquer` on a SQLite file opens a `<name> · Plan` tab showing the raw text; closing it returns to the query tab.

## Suggested commit message
Add the plan tab host and Expliquer / Analyser buttons to the WinUI head
