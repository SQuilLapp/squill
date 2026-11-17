# P17-08 - Avalonia head: filter bar, search box and footer

Status: done
Depends on: P17-06

## Goal
The same M05 strip as P17-06 in the Avalonia head: chips, `+ Filtre`, search box, footer. Recipe decisions 3, 4, 6; mockup M05.

## Design decisions (final)
- Host: `src/Squill.App.Avalonia/Views/QueryTabView.axaml` (+ `.axaml.cs`), results panel of P9-13, placed under the result tabs row and above the pending-edits banner, 36 high, same visibility rules as P17-06. Bind to `ResultViewModel.View` only (`Filters`, `SearchText`, `CanChange`, `BlockedReason`, `FooterText`, `ClearFilters`, `AddFilter` through the editor). Reuse the `kbd`, `ghost` and chip-like classes of `src/Squill.App.Avalonia/Styles/`; add a `filter-chip` style there once (1 px accent border, `accent-soft` fill, mono text 12, funnel and `x`); funnel and close icons from the existing icon resources (add the funnel path once if absent).
- `+ Filtre` opens a `Flyout`/`Popup` with the `FilterEditorViewModel` as in P17-06 (same fields, same keys, `Enter` applies, `Escape` closes, disabled with the `BlockedReason` tooltip when `CanChange` is false).
- Search box: 27 high, search icon, placeholder key, `Ctrl+F` kbd chip while empty, clear button when not; typing debounced 150 ms in the head (`DispatcherTimer`); `Ctrl+F` is a `KeyBinding` on `ResultsPanel` (the P8 pattern at `QueryTabView.axaml.cs:43`) focusing the box.
- Footer text from `View.FooterText`. Tokens only, `AutomationProperties.Name` from `UiText` on every control, tab order as in P17-06.
- An Avalonia headless test in `tests/Squill.App.Avalonia.Tests` (existing pattern): a result with a filter shows one chip with the expected text, the `x` removes it, the search box sets `SearchText`, the bar is disabled with pending changes.

## Out of scope
- Header marks and the column menu (P17-09).

## Tasks
- [x] Filter bar, chip style, flyout, search box, footer binding, headless test.

## Acceptance criteria
- Common acceptance with `Squill.slnx`; Avalonia AOT publish with zero IL2xxx/IL3xxx (`rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64`, in the background; run once and close).
- Captured in light and dark, `fr` and `en`, with a filter active, against M05.

## Suggested commit message
Add the grid filter bar, search box and footer to the Avalonia head

## To check by hand
- Same list as P17-06 (add/remove/clear filters, disabled with pending edits, `Ctrl+F` from the grid, narrow window, dark contrast); on Linux/macOS only if the owner has the machine.
