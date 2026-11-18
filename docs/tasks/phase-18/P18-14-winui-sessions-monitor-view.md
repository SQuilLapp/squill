# P18-14 - WinUI head: the sessions and locks view

Status: done
Depends on: P18-13, P9-10

## Goal
Draw M12 in the WinUI head. Recipe 5, 6.

## Design decisions (final)
- `Views/SessionsMonitorView.xaml(.cs)`: header (pulse glyph, title, engine badge, `connection / database`, environment pill, refresh text, `Pause P` button, filter box), the red top line on a Production connection (as in M12), the four tiles (sparkline drawn with a `Polyline` or Win2D), filter tabs, the sessions table, the detail pane (264 wide equivalent: use M12 numbers, crop to read), the `Qui bloque qui` panel with its collapsible chains, status bar segments.
- The sessions table is a Win2D / `Grid`-built control with fixed columns (`pid`, `Utilisateur`, `Application`, `Client`, `État`, `Durée` with sort arrow, `Attente`, `Requête`), row height from M12, selected row `accent-soft`, **no object `ItemsSource`** (recipe 5). The query column truncates with an ellipsis.
- Colors from tokens: state dots (`success`, `warning`, `fg-3`), durations (`warning`, `danger`), danger tile tint.
- Keys: `P` pause, `Ctrl+F` focus the filter, `Entrée` on a row focuses the detail, `Del` = terminate (asks the confirmation).
- Menu, explorer context and palette entries wired.

## Out of scope
- Avalonia (P18-15).

## Tasks
- [x] View, table control, sparkline, chain panel, wiring.
- [x] Tests in `tests/Squill.App.WinUI.Tests` where the harness allows (row count, selection by id, no `ItemsSource` of objects).

## Acceptance criteria
- Common acceptance (recipe 7) with the WinUI AOT publish; live capture needs a server: use the container of P18-11 (`-Keep`) with a blocked update, compare with M12 light and dark, list differences; what cannot be driven is "to check by hand".

## Suggested commit message
Add the WinUI sessions and locks view

## Report
- Live capture against a server (container of P18-11, blocked update) and the light/dark comparison with M12 were **not driven**: to check by hand. Known differences from M12: the query box in the detail pane is plain monospace (no syntax colors), the `pg_cancel_backend` hint on the cancel button is not drawn, the engine badge and pulse glyph are text, the table builds at most 400 row elements.
- Added catalog keys `activity.col.*`, `activity.session`, `explorer.sessionsMonitor`; WinUI AOT publish: zero IL2xxx/IL3xxx, the exe starts.
