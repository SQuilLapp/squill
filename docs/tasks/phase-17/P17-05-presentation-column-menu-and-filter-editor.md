# P17-05 - Presentation: column menu and filter editor view-models

Status: todo
Depends on: P17-02, P17-04

## Goal
Everything the M05 column menu and the `+ Filtre` editor show and do, as view-models both heads bind without logic of their own. Recipe decisions 4, 5, 7.

## Design decisions (final)
- `src/Squill.Presentation/Results/ColumnMenuViewModel.cs` (`public sealed partial class ColumnMenuViewModel : ViewModelBase`), created by `ResultViewState.OpenColumnMenu(int column)` (add that method here) and discarded on close. Members: `ColumnName`, `bool CanSort`, `GridSort? CurrentSort` for this column, commands `SortAscending`, `SortDescending`, `CopyName` (through the existing clipboard service the grid copy uses; if none is reachable from Presentation, expose `string ColumnName` and let the head copy, and say so in the report), `ValueSearch` (text, filters the list case-insensitively), `IReadOnlyList<ValueItem> Values` (`ValueItem`: `object? Value`, `string Text` (formatted, NULL shown as the `NULL` caption of the grid), `int Count`, `bool IsChecked`), `bool ValuesTruncated`, `string? ValuesHint` (`500 premières valeurs`), `ApplyValues` (builds one `In` filter from the checked items and replaces an existing `In` or `Equals` filter of that column; all checked = removes the column's value filter), `ClearColumnFilter`. The value list is computed by `ColumnValueCounts.Compute` (cap 500) on a worker with a cancellation when the menu opens (`IsLoadingValues` flag), checked state starts from the column's current `In` filter (nothing checked = everything checked when no filter exists).
- `src/Squill.Presentation/Results/FilterEditorViewModel.cs`: the `+ Filtre` popup. `IReadOnlyList<ColumnChoice> Columns`, `SelectedColumn`, `IReadOnlyList<OperatorChoice> Operators` (from `GridFilterMatcher.Operators`, each with its `UiText` name: `results.filter.op.equals` ... `results.filter.op.in`, fr and en: `est égal à`, `est différent de`, `contient`, `commence par`, `se termine par`, `>`, `>=`, `<`, `<=`, `est NULL`, `n'est pas NULL`, `est dans`), `SelectedOperator` (resets to the first operator when the column changes), `OperandText`, `bool NeedsOperand`, `string? Error` (from `TryCreate`), `Apply` (adds the filter to `ResultViewState` when valid, otherwise sets `Error` and keeps the popup open), `Cancel`. `Apply` on the `In` operator is not offered here (the value list of the column menu makes sets): `In` is excluded from this editor's operator list.
- Both view-models are disabled (`CanChange == false` of P17-04) with the same `BlockedReason`.
- `UiText` keys added here (fr + en, parity green): `results.filter.op.*` (12), `results.columnMenu.sortAsc`, `sortDesc`, `filterByValue` (`FILTRER PAR VALEUR`), `searchValue` (`Rechercher une valeur`), `clearFilter`, `copyName`, `valuesTruncated`, `results.filter.editor.title`, `results.filter.editor.column`, `results.filter.editor.operator`, `results.filter.editor.value`, `results.filter.editor.apply`. Freeze, hide and fit width have no key (not built).

## Out of scope
- Drawing (P17-06 ... P17-09), freeze / hide / fit width, OR groups.

## Tasks
- [ ] The two view-models, `ResultViewState.OpenColumnMenu`, catalog keys.
- [ ] Tests `tests/Squill.Presentation.Tests/Query/ColumnMenuViewModelTests.cs`, `FilterEditorViewModelTests.cs`: sort commands go through `ToggleSort` rules (ascending then descending on the same column), value list counts and NULL entry, `ValueSearch` narrows the list, `ApplyValues` builds the `In` filter with `null` when the NULL row is checked, applying all values removes the filter, an existing filter pre-checks its values, `ClearColumnFilter`, truncated hint, the editor lists operators per column category, switching column resets the operator, a bad number sets `Error` and adds nothing, `IsNull` needs no operand, both blocked with pending changes.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched.

## Suggested commit message
Add the column menu and filter editor view-models
