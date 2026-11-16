# P17-07 - WinUI head: header marks and the column menu in the Win2D grid

Status: done
Depends on: P17-06

## Goal
The Win2D grid draws the sort arrow, the filter funnel and a chevron in the column headers, and opens the M05 column menu. Recipe decisions 4, 5, 7; mockup M05 (`raison_sociale ↑`, `ville` with funnel and chevron, the menu card).

## Design decisions (final)
- Grid: `src/Squill.App.WinUI/Controls/` (the Win2D data grid of P5-10 / P9-16). Header drawing follows P9-16 (type under the name, primary key diamond): after the name draw, in this order, the sort arrow (up or down, `fg-1`) when `View.Sort` targets the column, the funnel (`accent`) when `View.Filters` has a filter on the column; a chevron button (12 px, hover tint) at the right edge of the header, shown on hover and while the menu is open. The grid reads the sort and filtered columns through a small read-only surface on the grid control (a property set by `QueryTabView` from `View`, redraw on `PropertyChanged`); the grid control itself never knows `GridFilter`.
- Opening: click on the chevron or right click on the header (the existing header click selects a whole column: keep it; a click on the chevron zone must not select). Raises an event `ColumnMenuRequested(int column, Point anchor)`; `QueryTabView` calls `View.OpenColumnMenu(column)` and shows a `Flyout` anchored under the header cell.
- Menu content (`ColumnMenuViewModel`, P17-05), 300 wide, radius 8, `bg-overlay`, `shadow-3` through the shared flyout style of P9-06: `Trier croissant` / `Trier décroissant` (current sort checked), a separator, caps title `FILTRER PAR VALEUR`, the value search box, a virtualized list of rows (checkbox + value text + right-aligned count in `fg-3`, NULL in italic `fg-3`), `ValuesHint` when truncated, a loading row while `IsLoadingValues`, a separator, `Effacer le filtre` (only when the column has one) and `Copier le nom` (with the `Ctrl+C` kbd chip only if the shortcut really copies the column name; otherwise no chip). The list applies on `Appliquer` (primary, bottom right) or `Enter`; `Escape` closes without applying. Menu items are disabled with the tooltip `BlockedReason` when `CanChange` is false (the header marks still show).
- Sorting by header click is **not** added (the click selects the column); sorting is through the menu and its keyboard path.
- Everything through the view-model and `UiText`; the grid control gets only drawing and the request event.

## Out of scope
- Freeze / hide / fit width, Avalonia (P17-09).

## Tasks
- [x] Header marks in the Win2D grid, chevron hit testing, `ColumnMenuRequested`, flyout in `QueryTabView`.
- [x] Tests where the head has a pattern; the rest under "To check by hand".

## Acceptance criteria
- Common acceptance with `Squill.Windows.slnx`, WinUI AOT publish with zero IL2xxx/IL3xxx, exe run once and closed.
- Captured in light and dark with a sort and a filter active and the menu open, against M05.

## Suggested commit message
Add sort and filter marks and the column menu to the WinUI grid

## To check by hand
- Chevron hover and click versus column selection; right click on the header; value list with 500 distinct values scrolls smoothly; the flyout stays inside a small window; keyboard use of the list (arrows, space to check, `Enter` applies); NULL row; dark theme contrast.
