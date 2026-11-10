# P13-17 - WinUI head: plan graph canvas

Status: done
Depends on: P13-16, P13-13

## Goal
The `Graphique` view of the WinUI plan tab: node cards, pills and edges drawn from `PlanLayout` on Win2D, with selection, zoom and pan. Follow `Controls/ErDiagramCanvas.cs` (Win2D `CanvasControl`, invalidation, pointer handling, `IDisposable`), and `Controls/GridTint`/`Views/Tokens.cs` for brushes.

## Design decisions (final)
- `Controls/PlanGraphCanvas` (new): draws `PlanLayoutResult` scaled by `Plan.Zoom`: dotted background like the ER canvas (spec section 9: 16 px dots `border-subtle` on `bg-base`), edges as Bezier curves (width = `Thickness`, color by severity: `border-strong` normal, `warning` alert, `danger` critical, 55 % opacity as in the mockup), pills (dashed 1 px border, mono text `fg-2`), cards (radius 8, `bg-raised`, 1 px `border`, amber or red 1.5 px border by severity, 3 px outer glow ring on the critical one): icon by `PlanNodeKind` (reuse `Icons.cs` glyphs; add the few missing in that file: scan, index, join, aggregate, sort, limit, filter), name 13 semibold, target mono 12 `fg-2` trimmed with ellipsis, 4 px cost bar (hidden when `RelativeShare` is null), `64 %` left in the severity color and the time right (mono 12), then the extra lines: `est. 9 812 · réel 5,79 M ×590` (only with a misestimate), the first warning text (amber, 12). Selected card gets a 2 px `accent` border; hit test = card rectangle, click selects (`Plan.SelectedRow`), the selected row of the table scrolls into view and vice versa.
- Zoom: `Zoom` property (P13-12) plus Ctrl+wheel; pan by dragging the background or middle button and scrollbars; `Ajuster` (zoom to fit) is the `ResetZoomCommand` when the plan is wider than the viewport (document: reset = fit width, max 100 %). The legend (bottom right, `Épaisseur = lignes`, two colored squares `> 30 % ou alerte`, `> 60 %`) is a XAML overlay, not drawn on the canvas. Drawing is clipped to the viewport (400+ node plans stay smooth); text layouts are created lazily and cached per node id, disposed on plan change and on `Unloaded`.
- Dark and light follow the theme through the token brushes at draw time.

## Out of scope
- Collapse/expand, drag of nodes, export of the graph, Avalonia.

## Tasks
- [x] Canvas, icons, legend, bindings, interaction.
- [ ] (not done, see report) Capture light and dark with the PostgreSQL ANALYZE plan and compare with M06 (cards, edges, pills, legend, glow); SQLite plan too (no bars/percent/time/extra lines; flat edges). Differences listed in the report.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI AOT publish zero IL2xxx/IL3xxx (Win2D under AOT is already proven by the grid and ER canvases).
- To check by hand: wheel zoom, drag pan, click selects card and row, 300-node plan (`select` with many unions) scrolls smoothly.

## Suggested commit message
Draw the query plan graph in the WinUI head
