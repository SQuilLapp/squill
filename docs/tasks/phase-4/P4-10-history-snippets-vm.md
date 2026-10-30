# P4-10 - Presentation: history and snippets view-models

Status: done
Depends on: P4-09

## Goal
Side panels to search past queries and manage snippets.

## Design decisions (final)
- `HistoryViewModel(IQueryHistoryStore, WorkspaceViewModel, IDialogService)`: `Entries` (newest first, max 500 loaded), `FilterText` (case-insensitive contains on SQL), `LoadCommand`, `ClearCommand` (confirm), `OpenCommand(entry)` opens a new tab with the SQL (no auto-run).
- `SnippetsViewModel(ISnippetStore, WorkspaceViewModel, IDialogService)`: `Snippets`, `SaveCurrentCommand` (asks a name through `PromptAsync`; uses the selection or the whole active tab text), `InsertCommand` (replaces the selection of the active editor), `DeleteCommand` (confirm), `RenameCommand`.
- Both are exposed on `AppServices`.

## Out of scope
- Folders, tags, import/export.

## Tasks
- [x] Implement both view-models and expose them from `AppComposition`.
- [x] Tests with fakes: filter, ordering, open, save/insert/delete.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add history and snippets view-models
