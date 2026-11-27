# P21-51 - Presentation: JSON value as a collapsible, searchable tree

Status: todo
Depends on: none

## Goal
A JSON cell value can be viewed as a tree (objects and arrays collapsible, search, copy path or value), as DBeaver's and DataGrip's value viewers do. The owner works with GeoJSON in `jsonb` cells, often large.

Exists today: `CellPreviewViewModel` (preview pane): JSON detection (`IsJson`, `LooksLikeJson`, column kind `CellEditorKind.Json`), pretty-printing with `Utf8JsonWriter`, and since **P20-43 the shown text is capped** (`MaxShownChars = 10_000`, `MaxShownLines = 2_000`, `IsTruncated`, `Show all`) so a very long value no longer freezes the UI thread; copy and apply keep the whole value.

## Design decisions (final)
- `JsonTreeViewModel` (Presentation/Results): built from the **whole** value (not the capped text) with `JsonDocument.Parse` **off the UI thread**; node view-models created **lazily** when a node is expanded (a root array of 100,000 features must not create 100,000 rows up front); children of a node shown by pages of 500 with a `Show 500 more` row.
- Node: key or index, kind (object, array, string, number, bool, null), preview (`{12}`, `[3]`, a string capped to 200 chars), `Path` (`$.features[3].geometry.type`).
- Search: by key or value substring, run off the UI thread over the document (not the created nodes), results as paths; selecting a result expands the path. Capped at 1,000 hits.
- Size guard: above 50 MB the tree is not built (message with the size), the text view (P20-43) stays.
- `CellPreviewViewModel` gets `ViewMode` (`Text` | `Tree`, default `Tree` for JSON values) and exposes the tree; editing stays in the text view (the tree is read-only).
- Commands: copy value, copy path, copy subtree as JSON.

## Out of scope
- Heads (P21-52, P21-53), editing in the tree, JSONPath queries.

## Tests and tasks
- [ ] Tests: lazy creation (child count created on expand only), paging, search hits and expansion, 50 MB guard, invalid JSON falls back to text, the P20-43 tests unchanged.
- [ ] Performance test: a 20 MB GeoJSON parses off the UI thread and the first level is ready under 1 s on the test machine (record the time).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- `JsonDocument` memory is about the size of the UTF-8 input: dispose it when the preview changes value.

## Open points (defaults)
- 50 MB guard (default); the owner may want another value.

## Suggested commit message
Show JSON values as a lazy, searchable tree in the preview pane view-model
