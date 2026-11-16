# P17-04 - Presentation: filter, search and sort state on the result (risky)

Status: todo
Depends on: P17-03

## Goal
`ResultViewModel` owns the filters, the search text and the sort of its tab, switches `DisplayGrid` to the filtered view, refuses edits while the view is active, and exposes what both heads draw (chips, funnel, counts, footer text). Recipe decisions 2, 3, 8, 9 and risk "row identity".

## Design decisions (final)
- New `src/Squill.Presentation/Results/ResultViewState.cs` (`public sealed partial class ResultViewState : ViewModelBase`), created by `ResultViewModel` for every grid result (`Kind == Grid`), exposed as `ResultViewModel.View`. Members: `ObservableCollection<FilterChip> Filters` (`FilterChip(GridFilter Filter, string Text)`, text from `GridFilterText`), `string SearchText` (settable; changes apply after a 150 ms debounce through `IUiDispatcher`-free means: apply immediately in the view-model and let the heads debounce typing, so tests stay deterministic), `GridSort? Sort`, `bool IsActive` (any filter, search or sort), `bool CanChange` (false while the result has pending changes or `ReadOnlyReason == "loading"` is irrelevant: filtering a streaming result is allowed), `string? BlockedReason`, `string FooterText`, and commands `AddFilter(GridFilter)`, `RemoveFilter(FilterChip)`, `ClearFilters()` (also clears search and sort), `SetSort(GridSort?)`, `ToggleSort(int column)` (P17-02 cycle). `FooterText`: unfiltered `Lignes 1-{0} sur {1} chargées` (keep the existing text and key if the footer already has one, reuse it), filtered `{0} lignes sur {1} chargées · filtre actif` (plural through `UiText.FormatPlural`), complete results drop `chargées`.
- `ResultViewModel.DisplayGrid` becomes: the `FilteredGridDataSource` while `View.IsActive`, otherwise `Editor?.Grid ?? Grid` as today; raise `PropertyChanged(DisplayGrid)` whenever it changes (the heads already re-bind on that, `CellPreviewViewModel` too). The view wraps the **inner display grid** (`Editor?.Grid ?? Grid`); when the editor is set later (`SetEditor`) while a view is active the view is rebuilt on the new inner grid.
- **Edit guard (decision 3).** `GridEditController` and `ResultEditorViewModel` must refuse every edit, add, delete, revert and "set NULL / DEFAULT" while `View.IsActive`, with the existing "read-only" path the heads already draw: `ResultViewModel.ReadOnlyReason` becomes `filtered` while the view is active and goes back to its previous value (`null`, a real reason or `loading`) when cleared (keep the previous value in a field; never lose a real reason). Add the `UiText` mapping of the reason (`results.readOnly.filtered`: `Lecture seule · filtre actif` / `Read-only · filter active`) where the other reasons are mapped (grep `results.readOnly` or the `ReadOnlyReason` consumers; both heads share that mapping). Conversely `CanChange` is false and `BlockedReason` = `Validez ou annulez les modifications avant de filtrer` (`results.filter.blocked`) while `HasPendingChanges`; `AddFilter`, `SetSort`, `ToggleSort` and a non-empty `SearchText` do nothing then. A pending change that appears while a view is active cannot happen (editing is refused); a commit or discard just raises `CanChange`.
- **Selection.** Every change of the view (`Apply`) clears `Selection` (`GridSelection.Clear`) and the cell preview follows through `DisplayGrid` changing; copy and the selection sum read `DisplayGrid`, so they copy what is shown.
- **Export** is unchanged (recipe decision 8): `ExportAsync` still exports `Grid`; the export menu title in the heads keeps `Grid.RowCount`. State this in the code comment of `ExportAsync` (one line).
- The view state is disposed with the result (`ResultViewModel` has no `Dispose` today: if the tab disposal path cannot reach it, the `FilteredGridDataSource` is released when the result is replaced; check how `QueryTabViewModel` drops old results and dispose there).
- `UiText` keys for this card (fr + en, both catalogs, parity green): `results.footer.filtered`, `results.readOnly.filtered`, `results.filter.blocked`, `results.filter.clear`, `results.filter.add` (`+ Filtre` / `+ Filter`), `results.filter.search.placeholder` (`Rechercher` / `Search`), `results.filter.remove` (tooltip). The operator names and the column-menu texts belong to P17-05.

## Out of scope
- The column menu and filter editor view-models (P17-05), any head code, persisting a view.

## Tasks
- [ ] `ResultViewState`, `ResultViewModel` integration (`View`, `DisplayGrid`, `ReadOnlyReason`, `Selection` reset), edit guard in `GridEditController` / `ResultEditorViewModel`, catalog keys.
- [ ] Tests in `tests/Squill.Presentation.Tests/Query/ResultViewStateTests.cs` (patterns of the existing `ResultViewModel` and `GridEditController` tests): adding a filter swaps `DisplayGrid` and raises the change, clearing restores the same grid instance, search, sort toggle cycle, footer text for filtered and unfiltered (fr and en), selection cleared on every change, the edit controller refuses a cell edit / add / delete while active and works again after `ClearFilters`, `ReadOnlyReason` restores its previous value (including `loading` and a real reason), a pending change blocks `AddFilter` with the reason, an editor set later keeps an active view, export still writes all rows of `Grid` while a filter is active.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched (the heads are not yet bound; nothing draws a chip until P17-06 / P17-08).
- Stop rule: if refusing edits cannot be done in one place (the edit entry points are scattered in the heads), stop and report with a proposed card fix; the guard must live in Presentation, never only in a head.

## Suggested commit message
Keep filter, search and sort state on the result and make the grid read-only while it is active
