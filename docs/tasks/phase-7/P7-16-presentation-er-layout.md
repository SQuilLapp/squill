# P7-16 - Presentation: ER diagram node layout

Status: done
Depends on: P7-15

## Goal
Deterministic placement of entity boxes, with a UI-independent measuring hook.

## Design decisions (final)
- Namespace `Squill.Presentation.Diagram`. `readonly record struct ErPoint(double X, double Y)`; `readonly record struct ErBox(double X, double Y, double Width, double Height)` with `Right`, `Bottom` and `Contains(ErPoint)`; `sealed record ErLayoutOptions(Func<string, double> MeasureText, double HeaderHeight = 28, double RowHeight = 20, double Padding = 12, double MinWidth = 160, double HorizontalGap = 96, double VerticalGap = 32, double MaxRowWidth = 2400)`. The heads pass a `MeasureText` backed by their text engine; tests pass `s => s.Length * 7`.
- `ErLayout.Compute(ErModel model, ErLayoutOptions options) : IReadOnlyDictionary<string, ErBox>` (key -> box, keys case-insensitive).
- Box size: width = `max(MinWidth, 2*Padding + max(measure(entity name), max over columns of measure("<name>  <type>")))` rounded up to a whole number; height = `HeaderHeight + RowHeight * Columns.Count`.
- Algorithm (all steps deterministic; ties broken by entity key, ordinal, case-insensitive):
  1. Split entities into connected components over relations (undirected). Entities with no relation (and all of them when there are no relations) form the **isolated** group.
  2. In each component compute a layer per entity: referenced (parent) tables go to the left. Layer = longest path from a table with no outgoing foreign key to a table in the component, over the relation graph with self-references ignored and cycles broken by dropping the back edges found by a depth-first search visiting entities in key order. Parents therefore have a smaller layer than their children.
  3. Order entities inside each layer with 4 barycenter sweeps (down, up, down, up) on the average position of their neighbours in the adjacent layer, starting from key order; entities with no neighbour there keep their slot.
  4. Place layer `i` at `x = sum of (max box width of layers < i) + i * HorizontalGap`, entities of a layer stacked from the top with `VerticalGap`, each layer centred vertically on the tallest layer of its component.
  5. Components are placed one below the other, separated by `2 * VerticalGap`, ordered by their first key; a component wider than `MaxRowWidth` is still one row (no wrapping).
  6. The isolated group goes below all components in a grid with `ceil(sqrt(n))` columns, cell size = the largest box of the group plus the gaps, filled row by row in key order.
- Origin: the top-left of the whole drawing is (0, 0).
- Layout is pure; saved positions are applied by the view-model (P7-19), not here.

## Out of scope
- Edge routing (P7-17), user dragging, persistence.

## Tasks
- [x] Implement the options, box types and `ErLayout`.
- [x] Tests in `tests/Squill.Presentation.Tests`: a chain A<-B<-C gives increasing x; a table referenced by two children is left of both; a cycle does not hang and yields distinct boxes; no two boxes overlap on random-ish fixed models (a 60-table synthetic schema built in the test); the isolated grid shape; the output is identical on two runs; widths follow `MeasureText`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add ER diagram node layout
