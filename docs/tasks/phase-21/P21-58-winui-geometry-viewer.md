# P21-58 - WinUI: geometry viewer on a Win2D canvas

Status: todo
Depends on: P21-57

## Goal
Draw `GeometryPreviewViewModel` in the WinUI head: in the preview pane for one cell, and in a result tab for a whole column.

Exists today: Win2D canvases (`DataGridCanvas`, `ErDiagramCanvas`, `PlanGraphCanvas`) with pan/zoom patterns.

## Design decisions (final)
- `GeometryCanvas` (Win2D): polygons filled with a translucent accent, outlines, lines, points as small circles, a scale bar for `WebMercator`; wheel zoom around the pointer, drag to pan, `Fit`; click selects a feature and shows its properties in a side list.
- `Map` toggle in the preview pane for GeoJSON/WKT values; `View as map` in the column menu for the column mode.

## Out of scope
- Avalonia (P21-59).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3) with a GeoJSON FeatureCollection of ~1 MB in the SQLite scenario (polygons with holes, lines, points) and on a PostgreSQL `jsonb` column.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- Win2D geometry creation per frame is slow: build `CanvasGeometry` once per zoom level change, not per draw.

## Open points (defaults)
- None.

## Suggested commit message
Draw geometries on a Win2D canvas in the WinUI head
