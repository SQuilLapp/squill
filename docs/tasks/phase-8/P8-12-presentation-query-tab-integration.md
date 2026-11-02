# P8-12 - Presentation: editable results in query tabs

Status: done
Depends on: P8-02, P8-10, P8-11, P4-09, P7-22

## Goal
A grid result that comes from a single table with a primary key gets a `ResultEditorViewModel`; everything else says why it is read-only. Opening a table from the explorer therefore yields an editable grid.

## Design decisions (final)
- `ResultViewModel` (read it) gains: `ResultEditorViewModel? Editor`, `string? ReadOnlyReason` (both settable internally, observable), `IGridDataSource? DisplayGrid` (`Editor?.Grid` when an editor exists, else `Grid`), and a `GridEditController? EditController` created together with the editor over a new `GridSelection` owned by the result (the heads take the controller and selection from the result so that both heads share them). The `Header` appends ` *` while `Editor.HasPendingChanges`.
- `QueryTabViewModel`: after `grid.StartAsync(ct)` completes for a result, call `EditableSourceAnalyzer.Analyze(original, dialect, session.Schema, grid.Columns)`. Target found -> create `EditableGridDataSource(grid, target)`, the editor (`isConnectionBusy: () => IsRunning`) and the controller, and set them on the result; otherwise set `ReadOnlyReason`. Message results get neither. Analysis failures (any exception) set `ReadOnlyReason = "analysis failed"` and are not rethrown. Heads must bind the grid to `DisplayGrid`; export (P4-11) keeps using the loaded `Grid` (pending changes are not exported, documented in the card report).
- The editor is only usable once the load finished (`EditableGridDataSource.CanEdit`); while loading, `ReadOnlyReason` is `"loading"` and is cleared when the editor becomes usable.
- Pending changes are never lost silently: running a query in a tab whose results have pending changes asks `ConfirmAsync("Run query", "Discard pending grid changes?")` first (declined = the run does not start); closing a tab asks `ConfirmAsync("Close tab", "Discard pending grid changes?")` when any result has changes (read how `WorkspaceViewModel` closes tabs and reuse that path; do not duplicate it); closing a session disposes its tabs as before (no extra prompt there).
- `SqlSnippets.SelectTop` is unchanged: `SELECT * FROM <table> LIMIT 100` (`TOP 100` for SQL Server) is accepted by the analyzer, so the explorer's open-table path needs no change; a test proves it.

## Out of scope
- Views (heads), editing joins or views, changing export.

## Tasks
- [x] Implement the `ResultViewModel` additions and the tab and workspace changes.
- [x] Tests in `tests/Squill.Presentation.Tests` (existing fixtures): editable result for a table with a primary key, read-only reasons for a join, a table without primary key, an aggregate, a view; `DisplayGrid`; header marker; run and close prompts (declined and accepted); explorer open-table gives an editable result; an end-to-end test with the real SQLite provider on a temp file: run `SELECT * FROM t`, edit a cell through `ResultEditorViewModel`, add a row, delete a row, commit, re-run the query and compare.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Make single-table query results editable
