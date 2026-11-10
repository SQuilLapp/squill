# P13-20 - Avalonia head: plan graph canvas

Status: todo
Depends on: P13-19, P13-17

## Goal
Twin of P13-17 for Avalonia: the `Graphique` canvas drawn from `PlanLayout`. Follow `Controls/ErDiagramCanvas.cs` of the Avalonia head (custom `Control` with `Render`, `DrawingContext`, pointer events).

## Design decisions (final)
- Same drawing rules as P13-17 (read it and copy: dotted background, Bezier edges with thickness and severity color, dashed pills, cards with icon, name, target, bar, percent, time, misestimate line, first warning, severity border and glow, 2 px accent border when selected, legend overlay as a normal control, zoom property plus Ctrl+wheel, pan by background drag, clipping to the viewport, lazy cached `FormattedText` disposed on plan change). Icons: Avalonia `Icons` geometry set of the head (add the scan/index/join/aggregate/sort/limit/filter paths there, same shapes as the WinUI glyphs).
- Rendering under Native AOT: use `DrawingContext.DrawGeometry`/`DrawRectangle`/`DrawText` only, `StreamGeometry` built per edge and cached per layout; no reflection-based features.

## Out of scope
- Collapse, node dragging, export.

## Tasks
- [ ] Canvas, icons, legend, interaction, selection sync with the table.
- [ ] Capture light and dark with the PostgreSQL ANALYZE plan against M06 and a SQLite plan; list differences.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green; Avalonia AOT publish zero IL2xxx/IL3xxx.
- To check by hand: wheel zoom, pan, click selects card and row, a 300-node plan stays responsive.

## Suggested commit message
Draw the query plan graph in the Avalonia head
