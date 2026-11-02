# P7-25 - WinUI head: ER diagram tab

Status: done
Depends on: P7-22, P7-20, P5-12

## Goal
The WinUI counterpart of P7-23, drawn with Win2D.

## Design decisions (final)
- `Controls/ErDiagramCanvas` using `CanvasControl.Draw` (`CanvasDrawingSession`), same drawing rules, markers, dashed optional edges, culling and input behaviour as P7-23 (pan, wheel zoom around the pointer, entity drag, click select, double click activate, `Ctrl+0` fit), all hit testing through `DiagramTabViewModel`. Text through `CanvasTextFormat`/`CanvasTextLayout` reusing the LRU cache pattern of `DataGridCanvas` (P5-10). Colours from the token resources used by the other WinUI controls.
- The `measureText` passed to the workspace by the WinUI head uses `CanvasTextLayout` with the canvas font. The head also creates `JsonDiagramLayoutStore(AppPaths)` where the other stores are created.
- `Views/DiagramTabView` with the same toolbar and bindings as P7-23 (`x:Bind`), tabs for `Diagrams` in the shell, `ER diagram` toolbar button, `Design table...` / `Drop table...` explorer context menu entries; `Designers` render a placeholder `Designer view arrives with P7-26` in this card, as in P7-23.
- Build and test with `Squill.Windows.slnx`.

## Out of scope
- WinUI designer view (P7-26), Avalonia changes.

## Tasks
- [x] Implement the control, the view and the shell integration.
- [x] Tests in `tests/Squill.App.WinUI.Tests` for the non-visual parts (control logic that can run without a window, e.g. the measure function and view-model wiring); run the AOT-published head on a SQLite file with related tables and check it starts and shows the window. State in the card report what was and was not exercised. Close every process you started.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- No process left running.

## Suggested commit message
Add ER diagram tab to the WinUI head

## Report
- Verified: `Squill.Windows.slnx` builds with 0 warnings; all tests green (938 passed, 44 Docker-gated skipped); AOT publish has no IL2xxx/IL3xxx warnings; the published exe starts with `SQUILL_DATA_DIR` and shows the `SQuilL` window; the XAML handler test now also covers `DiagramTabView` and the explorer `ContextRequested` handler.
- Not verified: opening the diagram, dragging, zooming and the explorer context menu in the running exe (no UI automation); the Win2D drawing is only compile-checked.
