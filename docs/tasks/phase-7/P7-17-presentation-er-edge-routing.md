# P7-17 - Presentation: ER diagram edge routing

Status: done
Depends on: P7-16

## Goal
Compute the polyline of every relation from the entity boxes, so heads only draw lines.

## Design decisions (final)
- Namespace `Squill.Presentation.Diagram`. `sealed record ErEdge(ErRelation Relation, IReadOnlyList<ErPoint> Points, ErPoint FromAnchor, ErPoint ToAnchor)`; `FromAnchor`/`ToAnchor` equal the first/last point and tell the head where to draw the cardinality markers.
- `ErRouter.Route(ErModel model, IReadOnlyDictionary<string, ErBox> boxes, ErLayoutOptions options) : IReadOnlyList<ErEdge>` (one edge per relation whose two entities both have a box, same order as `model.Relations`).
- Anchors: the vertical position is the centre of the row of the **first** foreign-key column in the child box and of the first referenced column in the parent box (`box.Y + HeaderHeight + RowHeight * (index + 0.5)`; row `index` is the position in `ErEntity.Columns`, fall back to the header centre when the column is not found).
- Sides: if `parent.Right < child.X` the edge leaves the child's left side and enters the parent's right side (the usual case, parents are laid out to the left); otherwise if `child.Right < parent.X` it leaves the child's right side and enters the parent's left side; otherwise (overlapping x ranges, and self-references) both ends use the right sides.
- Shape: orthogonal polyline. Opposite sides: `start`, `(midX, start.Y)`, `(midX, end.Y)`, `end` with `midX` halfway between the two facing edges. Same side (both right, or a self-reference): `start`, `(x + HorizontalGap/2, start.Y)`, `(x + HorizontalGap/2, end.Y)`, `end`, where `x` is the larger of the two right edges; a self-reference additionally offsets its two verticals by `RowHeight` per previous self-reference of the same entity so loops do not coincide.
- Several edges leaving the same side of the same box at the same Y are spread by 4 px increments in `midX` (edge index among them) so parallel segments do not overlap.
- Pure and deterministic; no UI types.

## Out of scope
- Avoiding crossings through other boxes (documented limit: lines may pass behind boxes), curved lines.

## Tasks
- [x] Implement `ErEdge` and `ErRouter`.
- [x] Tests in `tests/Squill.Presentation.Tests`: facing boxes (4 points, anchors at the right rows), reversed order, overlapping x, self-reference with two loops, composite key uses the first column, two edges to the same parent get different `midX`, missing box skips the edge.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add ER diagram edge routing
