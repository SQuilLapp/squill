# P7-23 - Avalonia head: ER diagram tab

Status: done
Depends on: P7-22, P7-20, P5-06

## Goal
Draw and operate the ER diagram on the Avalonia head, with the same view-model the WinUI head will use (P7-25).

## Design decisions (final)
- `Controls/ErDiagramCanvas` (a custom `Control` in `src/Squill.App.Avalonia/Controls/`, like the grid of P5-02): `Render` draws entities (body, header with name, one row per column with name, type and `PK`/`FK` markers), then edges (polylines from `DiagramTabViewModel.Edges`, dashed when optional, crow's-foot / bar markers at the anchors) under a transform `Translate(OffsetX, OffsetY) * Scale(Zoom)`. Only entities whose box intersects the viewport are drawn. Colours, radii, fonts come from the token resources used by the other controls; nothing hard-coded.
- Input: left drag on empty space pans (`Pan`), wheel zooms around the pointer (`ZoomAt`, factor 1.1 per notch), left drag on an entity calls `BeginDrag`/`DragTo`/`EndDrag`, click selects (`SelectedKey`, highlighted with the accent token), double click raises `ActivateCommand`, `Ctrl+0` = fit to screen. Hit testing only through `DiagramTabViewModel.HitTestEntity`/`ToWorld`; the control holds no layout logic.
- The `measureText` function given to the workspace by the head (in `AppComposition` call site) uses `FormattedText` with the control font, so layout widths match the drawing. Add the Avalonia `IDiagramLayoutStore` wiring (`JsonDiagramLayoutStore(AppPaths)`) at the same place the other stores are created.
- `Views/DiagramTabView` (`UserControl`, compiled bindings): toolbar with filter text box, `Include views` toggle, Fit, Reset layout, Focus (selected) with depth selector, Show all, Export SVG; the canvas below; status text. Sets `IsDarkTheme` from the current theme variant.
- Shell: add an `ER diagram` toolbar button (bound to `OpenDiagramCommand`, shown only when the command can execute) and show `Diagrams` in the tab strip next to query and terminal tabs, following how P6-02 added terminals (`MainWindow.Terminal.cs`); closing a diagram tab runs `CloseDiagramCommand`. The object explorer context menu gains `Design table...` and `Drop table...` entries for table nodes (they call the commands of P7-22; the designer view comes in P7-24, so until then `DesignTableCommand` opens a tab the head cannot yet render: render `Designers` items with a placeholder text `Designer view arrives with P7-24` in this card).

## Out of scope
- Designer view (P7-24), WinUI (P7-25, P7-26), touch gestures.

## Tasks
- [x] Implement the control, the view and the shell integration.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests` (Avalonia.Headless as in the other view tests): the view binds to a synthetic schema, dragging an entity moves its box, wheel changes `Zoom`, double click raises activation, export command is reachable.
- [x] Run the AOT-published head against a SQLite file with a few related tables (use `SQUILL_DATA_DIR` as in P5-04), open the diagram, check it starts and shows the window; record what could and could not be verified in the card report. Close every process you started.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings from Squill code (the tolerated package-level warnings of P5-04 stay as they are).
- No process left running.

## Suggested commit message
Add ER diagram tab to the Avalonia head

## Report
- Verified: build 0 warnings, all tests green (headless: view binds to the diagram, drag moves a box, wheel zooms, double click activates, Export SVG button bound to the command); AOT publish has only the tolerated package warnings; the published exe starts with `SQUILL_DATA_DIR` and shows the `SQuilL` window.
- Not verified: opening the diagram in the published exe (no UI automation; covered by the headless tests), and the explorer context menu (`Design table...`, `Drop table...`), which is only compile-checked.
