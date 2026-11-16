# P17-09 - Avalonia head: header marks and the column menu

Status: todo
Depends on: P17-07, P17-08

## Goal
The Avalonia custom-drawn grid draws the sort arrow, funnel and chevron and opens the same column menu as P17-07. Recipe decisions 4, 5, 7; mockup M05.

## Design decisions (final)
- Grid: `src/Squill.App.Avalonia/Controls/DataGridCanvas.cs` (header drawing of P9-14). Same marks, order, hover and hit rules as P17-07, fed by a read-only surface on the control (sorted column and filtered columns, set by the view, redraw on change); the control raises `ColumnMenuRequested(column, anchor)` for a chevron click or a right click; a click elsewhere on the header keeps selecting the column. Note: `DataGridCanvas` already contains the word "sort" for something unrelated, read it before adding.
- The menu is a `Flyout` anchored under the header cell in `QueryTabView.axaml(.cs)` bound to `ColumnMenuViewModel`: same content, same order, same disabled rule, `Appliquer` / `Enter` / `Escape` as P17-07, virtualized value list (`ItemsRepeater` or `ListBox` with virtualization), `Classes` from the shared styles; texts only through `UiText` keys of P17-05.
- Headless test: with a fake grid and `View`, a sort on a column shows the arrow state on the surface property, a menu request produces a `ColumnMenuViewModel` for that column, `Apply` with unchecked values adds one `In` filter.

## Out of scope
- Freeze / hide / fit width.

## Tasks
- [ ] Header marks, chevron hit testing, event, flyout and bindings, headless test.

## Acceptance criteria
- Common acceptance with `Squill.slnx`, Avalonia AOT publish with zero IL2xxx/IL3xxx, run once and close.
- Captured in light and dark with a sort and a filter active and the menu open, against M05.

## Suggested commit message
Add sort and filter marks and the column menu to the Avalonia grid

## To check by hand
- Same list as P17-07.
