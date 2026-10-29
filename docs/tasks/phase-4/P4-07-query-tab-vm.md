# P4-07 - Presentation: query tab and results view-models

Status: todo
Depends on: P4-02, P4-06, P2-06

## Goal
Run statements from an editor and expose results and messages.

## Design decisions (final)
- Namespace `Squill.Presentation.Query`. `QueryTabViewModel(ConnectionSession session, ISqlEditor editor, IQueryHistoryStore history, ISettingsStore settings, IUiDispatcher dispatcher, IDialogService dialogs)`: properties `Title`, `IsRunning`, `Elapsed`, `StatusText`, `Results` (`ObservableCollection<ResultViewModel>`), `SelectedResult`; commands `RunCommand` (selection or statement at caret), `RunAllCommand`, `CancelCommand`; editor wired with dialect, completion and diagnostics providers of `Squill.Sql` composed from `session.Schema` (refreshed after DDL statements and on `RefreshSchemaAsync`).
- `ResultViewModel` has a `Kind` (`Grid` or `Message`), `Title` (`Result 1`...), for grids a `StreamingGridDataSource` (`MaxRows` from settings), for messages `Text`. A statement with no columns produces a message `N row(s) affected` (or `Command completed`).
- Statements run sequentially, one `ExecuteAsync` each; the first error stops the run and adds a `Message` result with the exception message, previous results stay. Each run appends one `HistoryEntry` per statement. `Cancel` cancels the linked token; the running grid stream is disposed.
- A new run disposes the previous results' streams. `IsRunning` guards re-entrancy (second run while running is ignored).

## Out of scope
- Parameters (P4-08).
- Result export (P4-11).

## Tasks
- [ ] Implement the view-models.
- [ ] Tests with a fake provider/session: single statement, script of 3 statements, error in the second, cancel, history entries, DML message text, re-entrancy.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add query tab and result view-models
