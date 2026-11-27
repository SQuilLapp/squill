# P21-27 - Presentation: rename refactoring across open editors and snippets, with the DDL

Status: todo
Depends on: P21-26

## Goal
`Rename...` on a table or column (from the explorer, the designer or the editor caret) shows a preview of every change and, on confirmation, renames the object in the database and updates the open query tabs and the saved snippets. Script files on disk do not exist yet; P21-34 adds them as a further scope of this view-model.

Exists today: the designer generates `ALTER TABLE ... RENAME` per dialect (P7-11 ... P7-14) and applies it through the statement runner with the production confirmation; snippets in `ISnippetStore`.

## Design decisions (final)
- `RenameRefactoringViewModel`: target, new name (validated: not empty, not existing in the snapshot), scope check boxes (`Database object` on by default, `Open query tabs`, `Snippets`), built from a list of `IRenameSource` (open tabs, snippets) so P21-34 adds `Script files` without changing the view-model, and a preview list grouped by source: DDL statement (from the designer's generator), then each text with its edits (line, before, after) and the `Skipped` occurrences with their reason.
- Apply order: DDL first through `SqlRunner` + `ProductionRequestBuilder` (a failure stops everything); then text edits (open tabs as one undoable change each; snippets saved).
- A text with skipped occurrences is still edited for the resolved ones; the summary lists the skipped count.

## Out of scope
- Renaming views, routines, schemas. Heads (P21-28).

## Tasks
- [ ] Tests: preview content, DDL failure leaves texts untouched, production confirmation requested, open tab edited as one undo step, snippet saved.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- The rename DDL on SQLite may rebuild a table (P7-14): the preview shows the full script.

## Open points (defaults)
- Owner: should the history entries be renamed too (default no: history is a record of what ran).

## Suggested commit message
Rename a table or column in the database, open tabs and snippets with a preview
