# P17-06 - WinUI head: filter bar, search box and footer

Status: todo
Depends on: P17-04, P17-05, P9-15

## Goal
The M05 strip between the results header and the grid in the WinUI head: filter chips, `+ Filtre`, the `Rechercher` box with `Ctrl+F`, and the footer text of the active view. Recipe decisions 3, 4, 6; mockup M05.

## Design decisions (final)
- Host: `src/Squill.App.WinUI/Views/QueryTabView.cs` (code-built, results panel of P9-15). The bar sits under the result tabs row and above the pending-edits banner, 36 high, hidden when the result is a message; visible for every grid result (the `+ Filtre` button and the search box are always there, chips appear with filters). Bind only to `ResultViewModel.View` (P17-04): `Filters`, `SearchText`, `CanChange`, `BlockedReason`, `FooterText`, `ClearFilters`. No filter logic in the head.
- Chip: pill, 1 px `accent` border, `accent-soft` fill, funnel icon in `accent`, mono text 12 (the chip text), `x` button (tooltip `results.filter.remove`); tokens as in `visual-spec.md` section 3 / 6 and the shared styles of P9-06 (reuse the existing icon geometry for the funnel and `x` if present, otherwise add them once to the shared icon resources).
- `+ Filtre` opens a flyout hosting the `FilterEditorViewModel` (column `ComboBox`, operator `ComboBox`, value `TextBox`, error text in `danger`, `Appliquer` primary and `Annuler`); `Enter` applies, `Escape` closes. The flyout and the button are disabled with the `BlockedReason` tooltip when `CanChange` is false.
- Search box: 27 high, search icon, placeholder `results.filter.search.placeholder`, `Ctrl+F` chip as a `kbd` pill at the right (hidden when the box has text, an `x` clear button instead). Typing is debounced 150 ms in the head (a `DispatcherQueueTimer`) before it sets `View.SearchText`. `Ctrl+F` focuses the box when the results panel or the grid has focus (an `Accelerator` on the results panel, not a window-wide one: the editor keeps its own find if it has one; check and say in the report).
- Footer: the existing footer text is replaced by `View.FooterText`; the read-only marker already follows `ReadOnlyReason` (P17-04 maps `filtered`), nothing else to do there.
- Dark and light by the shared tokens only; no hard-coded color; keyboard reachable (tab order: chips' `x`, `+ Filtre`, search box), `AutomationProperties.Name` on every control from `UiText`.

## Out of scope
- Header marks and the column menu (P17-07), Avalonia (P17-08), freeze / hide / fit width.

## Tasks
- [ ] Filter bar, chip, `+ Filtre` flyout, search box and footer binding in the WinUI head.
- [ ] Tests only where `tests/Squill.App.WinUI.Tests` already has a pattern; otherwise "To check by hand" below.

## Acceptance criteria
- Common acceptance with `Squill.Windows.slnx`; WinUI AOT publish with zero IL2xxx/IL3xxx warnings (`rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64`, in the background, then run the exe once and close it).
- Captured in light and dark, `fr` and `en`, with a filter active, against M05 (chip, `+ Filtre`, search box, footer).

## Suggested commit message
Add the grid filter bar, search box and footer to the WinUI head

## To check by hand
- Add, edit-by-removing and clear filters; typed errors in the editor; the bar is disabled with pending edits; `Ctrl+F` from the grid; chip text clipped or wrapped on a narrow window; screen reader names; dark contrast of the chip.
