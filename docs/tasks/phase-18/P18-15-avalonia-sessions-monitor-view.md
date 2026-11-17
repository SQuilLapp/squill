# P18-15 - Avalonia head: the sessions and locks view

Status: done
Depends on: P18-14

## Goal
The same M12 view in the Avalonia head. Recipe 6.

## Design decisions (final)
- `Views/SessionsMonitorView.axaml(.cs)`; the table as a `DataGrid`-free custom control or an `ItemsControl` with a fixed column grid (follow what the plan table of P13-19 does); same numbers and token colors as WinUI; sparkline with `Polyline`.
- Keys as `KeyBinding`s; menu, explorer context and palette entries wired.

## Tasks
- [x] View, table, sparkline, chain panel, wiring.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests` (row count, selection, filter tab counts, both themes render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare with M12 as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia sessions and locks view

## Report
- Live capture against a server and the light/dark comparison with M12 were **not driven**: to check by hand. Known differences from M12 (same as WinUI): the query box in the detail pane is plain monospace (no syntax colors), the `pg_cancel_backend` hint is not drawn, the engine badge and pulse glyph are text, the table builds at most 400 row elements.
- Avalonia AOT publish: no IL2xxx/IL3xxx from our code (only the third-party `Microsoft.Data.SqlClient` assembly summaries, as before), the exe starts. Headless tests: row count, selection by id, filter tab counts, light/dark pixels, token mappings.
