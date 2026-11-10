# P13-13 - Presentation: plan graph layout

Status: done
Depends on: P13-12

## Goal
A pure layout function that places the graph of the mockup (operators flow left to right, root on the right, leaves on the left, helper nodes as edge pills) so both heads only draw rectangles, pills and curves. Look at how `ErLayout` / `ErRouter` in `Squill.Presentation/Diagram` take text measurement and return results, and follow the same style (injected `Func<string, double>` measurer, no UI types).

## Design decisions (final)
- `PlanLayout.Compute(IReadOnlyList<PlanRowViewModel> rows, Func<string, double> measureText)` returns `PlanLayoutResult(IReadOnlyList<PlanNodeBox> Nodes, IReadOnlyList<PlanPill> Pills, IReadOnlyList<PlanEdgePath> Edges, double Width, double Height)` (records, public, XML docs). Coordinates in logical px at zoom 1; the heads scale.
- Geometry (the numbers of the PNG at 1x): card width 250, card height 90 (name + target + bar + figures) + 20 per extra line (misestimate line, warning line, spill line; at most 2 extra lines, the rest only in details), horizontal gap between depth columns 70, vertical gap between sibling subtrees 30, outer margin 24.
- Column of a node = `maxDepth - Depth` (root at the right, depth measured **skipping helper nodes**: a helper is not a column). Vertical position: leaves take consecutive slots top to bottom in pre-order of the original tree; an inner node is vertically centered on its children (mean of first and last child). Siblings' order = tree order, so the first child is on top.
- Helpers (`IsHelper`): no `PlanNodeBox`; a `PlanPill(Id, Text, X, Y, Width, Height)` sitting on the edge midpoint between the helper's single child and its parent, text = `<name> <rows abbreviated: 84 k> · <memory if a property gives it>`; if the helper has no child or several, it is drawn as a normal card (defensive).
- Edge from child right-middle to parent left-middle as a cubic Bezier (`PlanEdgePath(FromId, ToId, X0, Y0, C1X, C1Y, C2X, C2Y, X1, Y1, double Thickness, PlanSeverity Severity)`) with horizontal control points at 50 % of the gap. `Thickness` = 1.5 + 6 * log10(1 + rows) / log10(1 + maxRows) clamped to [1.5, 7.5] where rows = the child's `ActualRows` else `EstimatedRows`, else 1.5 flat (SQLite). Edge `Severity` = severity of the child node (colors: normal/alert/critical, red edge in the mockup for the critical subtree).
- Text that does not fit is cut by the head with an ellipsis; layout never measures more than name, target and the abbreviated figure strings (so a 10 000-node plan stays fast: O(n)). A plan with more than 400 nodes still lays out (no cap) and the head virtualizes drawing by clipping to the viewport.

## Out of scope
- Drawing, panning, zooming, hit testing (heads), collapse of subtrees.

## Tasks
- [x] `PlanLayout` and result records.
- [x] Tests (`tests/Squill.Presentation.Tests/Plans/PlanLayoutTests.cs`): the shape of the mockup (10 nodes with two Hash helpers) gives 8 cards, 2 pills and right-most root; no two cards overlap (pairwise check) on a bushy random-but-seeded tree of 200 nodes; inner node centered; helper pill lies between the two cards; thickness monotonic in rows; single-node plan; SQLite-shaped plan without figures (flat thickness, no extra lines); determinism (same input same output).

## Acceptance criteria
- Common acceptance of the recipe, section 5.

## Suggested commit message
Lay out the query plan graph
