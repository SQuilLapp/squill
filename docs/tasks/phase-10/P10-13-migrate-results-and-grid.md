# P10-13 - Migrate strings: results, grid, editing and export

Status: todo
Depends on: P10-03, P10-04, P10-07, P10-08, P10-09

## Goal
The results panel, the data grid, the edit toolbar, pending-change banner, validation issues, commit / discard dialogs and the export menu are fully bilingual in both heads.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Area `results` of `inventory.md`.
- Files in scope (the inventory is authoritative): `src/Squill.Presentation/Query/ResultViewModel.cs`, `Editing/ResultEditorViewModel.cs` ("Rows are still loading", "Row is marked for deletion", "Commit blocked", "Delete N row(s) from T?", "Discard N pending change(s)?": `FormatPlural`), `Editing/ChangeSetBuilder.cs` (render `EditIssue` through `IssueText.Format`), `Results/EditableGridDataSource.cs`, `Results/CsvResultExporter.cs`, `Results/JsonResultExporter.cs`, `Results/GridBenchReport` n/a, results panel / edit toolbar / export menu / grid views and canvases of both heads (empty-state text, tooltips, context menus, column menu if present).
- Exporters and the editable data source throw `InvalidOperationException` with user-visible English text today. Final: the exceptions stay English, developer-facing guards; the view-model checks the condition first (result still loading, row marked for deletion) and shows the catalog message instead, so the guard text can never reach the user. Add tests that the guard path is not reachable from the UI commands (command `CanExecute` false or dialog shown).
- Exported files are data: CSV/JSON content, headers (column names), formatting stay invariant and language-neutral (see P10-07); no translated text is written into an export.
- The `NULL` marker and cell values are not translated.
- Number/date text in the grid is already handled by P10-07; do not touch it again.

## Out of scope
- Designer apply flow (P10-14), generic dialog button labels (P10-16).

## Tasks
- [ ] Migrate every `results` inventory line and tick it.
- [ ] Tests per recipe rule 7 (pending-change count plural both languages, delete confirmation, commit blocked, edit issues in both languages, exports identical under both languages).
- [ ] English captures: results with rows, empty result, pending-edit banner, an issue, export menu, both heads.

## Acceptance criteria
- Common criteria of the recipe.

## Suggested commit message
Localize the results panel, grid editing and export
