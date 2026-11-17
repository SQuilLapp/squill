# P18-15 - Avalonia head: the sessions and locks view

Status: todo
Depends on: P18-14

## Goal
The same M12 view in the Avalonia head. Recipe 6.

## Design decisions (final)
- `Views/SessionsMonitorView.axaml(.cs)`; the table as a `DataGrid`-free custom control or an `ItemsControl` with a fixed column grid (follow what the plan table of P13-19 does); same numbers and token colors as WinUI; sparkline with `Polyline`.
- Keys as `KeyBinding`s; menu, explorer context and palette entries wired.

## Tasks
- [ ] View, table, sparkline, chain panel, wiring.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests` (row count, selection, filter tab counts, both themes render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare with M12 as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia sessions and locks view
