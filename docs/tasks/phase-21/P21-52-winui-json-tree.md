# P21-52 - WinUI: the JSON tree in the preview pane

Status: todo
Depends on: P21-51

## Goal
Show the JSON tree of P21-51 in the WinUI preview pane, with a `Text | Tree` switch.

Exists today: `Views/CellPreviewPanel.cs` (P20-40 ... P20-43: capped text, `Show all`, theme-following brushes).

## Design decisions (final)
- A segmented `Text | Tree` toggle in the pane header; the tree as a virtualized list of flattened visible rows (indent, chevron, key in mono, value preview coloured by kind with tokens), not a `TreeView` (virtualization and AOT safety); search box above it; context menu with the copy commands.
- Brushes through `Tokens.Brush` / theme repaint (P20-42 guard).

## Out of scope
- Avalonia (P21-53).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3): a 5 MB GeoJSON `jsonb` cell on PostgreSQL (or a TEXT cell on SQLite) - open, expand `features`, page, search `Polygon`, copy path; measure that the window stays responsive (the driver's response time).

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- Row height and scroll with paging rows: keep one row height.

## Open points (defaults)
- None.

## Suggested commit message
Show the JSON tree in the WinUI preview pane
