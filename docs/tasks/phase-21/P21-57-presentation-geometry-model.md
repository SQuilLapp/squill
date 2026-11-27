# P21-57 - Presentation: geometry and GeoJSON model for a map-like preview

Status: todo
Depends on: P21-51

## Goal
The owner works with GeoJSON: a GeoJSON cell (or a whole result column of geometries) can be drawn as shapes on a simple canvas, like DBeaver's spatial viewer, without any map tiles.

Exists today: JSON detection and the JSON tree (P21-51); spatial types are "not supported" (ADR 0008): PostGIS `geometry`, SQL Server `geometry`/`geography`, MySQL spatial values are not readable as such by the AOT-safe drivers (shown `<unsupported ...>` or as bytes).

## Design decisions (final)
- `GeometryParser` (pure): GeoJSON `Point`, `MultiPoint`, `LineString`, `MultiLineString`, `Polygon` (with holes), `MultiPolygon`, `GeometryCollection`, `Feature`, `FeatureCollection` (properties kept per feature); WKT text of the same kinds (what `ST_AsText` returns). WKB is not parsed in this card.
- `GeometryPreviewViewModel`: the parsed shapes, bounds, a projection (`Planar` for unknown CRS, `WebMercator` when coordinates look like longitude/latitude: all x in -180..180 and y in -90..90), pan/zoom state, the selected feature and its properties, a count cap (100,000 vertices drawn; beyond, simplified by stride with a notice).
- Column mode: `ResultViewModel.ShowGeometryCommand(column)` gathers the column's loaded values into one preview (each row a feature, row number as id).
- Hint in the UI: for PostGIS, `SELECT ST_AsGeoJSON(geom)` gives a readable value (the native type is not readable).

## Out of scope
- Heads (P21-58, P21-59), map tiles / basemaps (would mean network calls: open point), CRS reprojection, WKB.

## Tasks
- [ ] Parser tests (every kind, holes, invalid input), bounds, projection choice, vertex cap.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Very large FeatureCollections: parse off the UI thread with the P21-51 document.

## Open points (defaults)
- Owner: a basemap from an online tile server (OpenStreetMap) would send the viewed area to a third party; default **no basemap** (privacy rule, recipe 6).

## Suggested commit message
Parse GeoJSON and WKT geometries for a canvas preview
