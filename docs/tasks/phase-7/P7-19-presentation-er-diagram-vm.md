# P7-19 - Presentation: ER diagram view-model

Status: todo
Depends on: P7-15, P7-16, P7-17, P7-18, P4-04

## Goal
The headless view-model of a diagram tab: scene, selection, pan/zoom, dragging, persistence.

## Design decisions (final)
- Namespace `Squill.Presentation.Diagram`. `DiagramTabViewModel : ViewModelBase, IDisposable` created with `(ConnectionSession session, IDiagramLayoutStore store, IDialogService dialogs, Func<string, double> measureText, IUiDispatcher dispatcher)`; `Title` = `ER - <profile name>`.
- State (observable properties): `ErModel Model`, `IReadOnlyDictionary<string, ErBox> Boxes`, `IReadOnlyList<ErEdge> Edges`, `string? SelectedKey`, `double Zoom` (1.0, clamped 0.1 to 4.0), `double OffsetX`, `double OffsetY`, `bool IncludeViews`, `string FilterText`, `int UnresolvedRelations`, `string? FocusKey` and `int FocusDepth` (1 to 3, default 1).
- Commands: `RefreshCommand` (calls `session.RefreshSchemaAsync`, rebuilds), `FitToScreenCommand(ErSize viewport)` (zoom and offset so the whole drawing fits with a 24 px margin, zoom clamped), `ResetLayoutCommand` (drops saved positions and recomputes), `FocusSelectedCommand` (filters to `ErModel.Neighborhood(SelectedKey, FocusDepth)`), `ShowAllCommand`, `OpenTableRequested` event (`TableInfo`) raised by `ActivateCommand(string key)`, and `DesignTableRequested` event raised by `DesignCommand(string key)` (consumed by P7-22). Define a tiny `readonly record struct ErSize(double Width, double Height)` for the viewport.
- Methods used by the heads: `ErEntity? HitTestEntity(ErPoint worldPoint)` (topmost box containing the point), `ToWorld(ErPoint screen)`/`ToScreen(ErPoint world)`, `ZoomAt(ErPoint screenAnchor, double factor)` (keeps the world point under the anchor fixed), `Pan(double dx, double dy)`, `BeginDrag(string key)`, `DragTo(ErPoint world)`, `EndDrag()`. Dragging moves only the box of that entity, recomputes `Edges` with `ErRouter` and, on `EndDrag`, saves the layout (debounced is not needed; one `SaveAsync` per drag end).
- `FilterText` (case-insensitive contains on entity name) hides non-matching entities (it filters the model, then layout is recomputed for the visible set; saved positions still apply to the entities that have one, the others are placed by `ErLayout` and then kept at their computed position).
- Initial load: build the model, compute `ErLayout`, override with saved positions from the store (`LoadAsync`; keys not in the model are ignored; a saved `Zoom`/`Offset` is restored), then route edges. Saving stores positions of every box plus zoom/offset.
- A schema with more than 200 tables opens with `FocusKey` unset but only the first 50 entities (ordered by relation count descending, then key) and `StatusText` says "Showing 50 of N tables, use the filter or Focus"; `ShowAllCommand` lifts the cap.
- `WorkspaceViewModel` integration is P7-22. No UI types.

## Out of scope
- Drawing (P7-23, P7-25), SVG export (P7-20), table editing (P7-21).

## Tasks
- [ ] Implement the view-model and `ErSize`.
- [ ] Tests in `tests/Squill.Presentation.Tests` with a fake store and a synthetic session (see how P4-05/P4-07 tests build a `ConnectionSession`): initial layout, restore of saved positions, drag then save, zoom around an anchor keeps the world point, fit-to-screen, filter, focus, 200+ table cap, hit test picks the topmost box, refresh rebuilds, events raised.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add ER diagram view-model
