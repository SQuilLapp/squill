# P7-22 - Presentation: workspace integration of designer and diagram tabs

Status: todo
Depends on: P7-19, P7-20, P7-21, P4-09, P6-01

## Goal
The workspace can open diagram and designer tabs and drop tables; the object explorer can request them.

## Design decisions (final)
- Read `WorkspaceViewModel` (how `Terminals` and `NewTerminalCommand` were added in P6-01) and follow the same shape. New collections `Designers` (`ObservableCollection<TableDesignerViewModel>`) and `Diagrams` (`ObservableCollection<DiagramTabViewModel>`); new commands (CommunityToolkit `[RelayCommand]`): `NewTableCommand` (designer on the active session, no-op without one), `DesignTableCommand(TableInfo?)`, `OpenDiagramCommand` (one diagram per session: activating again selects the existing one; property `ActiveDiagram`/`ActiveDesigner` like `ActiveTab`), `DropTableCommand(TableInfo?)` (confirm with `IDialogService.ConfirmAsync("Drop table", "Drop <qualified name>? This cannot be undone.")`, run `DdlGenerators.For(dialect).DropTable(...)` through the session connection, refresh the schema, close designers of that table, report errors through `ShowErrorAsync`), `CloseDesignerCommand`, `CloseDiagramCommand` (disposes).
- Constructor gains two **optional trailing parameters**, like `terminalHostFactory`: `IDiagramLayoutStore? diagramStore = null` and `Func<string, double>? measureText = null`. When either is null `OpenDiagramCommand` cannot execute (heads without a text measurer simply do not show the diagram). `AppComposition` gets the same optional parameters and passes them through; the existing call sites and tests keep compiling.
- Event wiring: a designer's `ScriptRequested` calls `OpenInNewTab(script)`; a diagram's `OpenTableRequested` calls the existing "select top rows" path used by the explorer (reuse, do not duplicate), its `DesignTableRequested` calls `DesignTableCommand`. Opening the designer for a table already open in a designer selects that designer.
- `ObjectExplorerViewModel` gains `DesignTableRequested` and `DropTableRequested` events (`TableInfo`) and `DesignCommand(ExplorerNode)` / `DropCommand(ExplorerNode)` that raise them for `Table` nodes only (views: no-op); the workspace subscribes when it creates the explorer.
- When a session is closed, its designers and diagrams are closed and disposed like its query tabs are.
- Unsaved designer changes: closing a designer with `HasChanges` asks `ConfirmAsync("Close designer", "Discard unsaved changes?")`.

## Out of scope
- The views (heads, P7-23 to P7-26).

## Tasks
- [ ] Implement the commands, collections, event wiring and `AppComposition` parameters.
- [ ] Tests in `tests/Squill.Presentation.Tests` using the existing workspace test fixtures: new table opens a designer, design table reuses an open one, diagram is unique per session, session close disposes both, drop confirmed / declined / failing, explorer commands raise events only for tables, closing a dirty designer asks.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add designer and diagram tabs to the workspace
