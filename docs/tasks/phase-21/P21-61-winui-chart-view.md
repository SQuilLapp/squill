# P21-61 - WinUI: chart view of a result

Status: todo
Depends on: P21-60

## Goal
A `Chart` view next to the grid of a result tab in the WinUI head.

Exists today: result tabs (grid, messages), Win2D canvases.

## Design decisions (final)
- `Grid | Chart` toggle in the result header; a left options strip (kind, X, Y check list, aggregation) and a Win2D `ChartCanvas` (axes, ticks, legend, hover tooltip with the value, series colours from the design tokens' categorical set or a new token group added to `tokens/squill.tokens.json` if none exists); `Export SVG`, `Export CSV`.

## Out of scope
- Avalonia (P21-62).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3): a grouped query on the SQLite scenario (sales per month) as bar, line, pie; dark and light.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- Adding tokens regenerates XAML for both heads: run the token generator tests.

## Open points (defaults)
- None.

## Suggested commit message
Show result charts in the WinUI head
