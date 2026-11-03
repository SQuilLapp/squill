# P10-10 - Migrate strings: connection editor, validation and connection list

Status: done
Depends on: P10-03, P10-04, P10-07, P10-08

## Goal
The connection editor, its validation messages (the English "Name is required." family and the French messages added for the WinUI SQLite picker), the connection list / manager and their dialogs are fully bilingual in both heads.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Area `connections` of `inventory.md`.
- Files in scope (the inventory is authoritative): `src/Squill.Presentation/Connections/*` (`ConnectionEditorViewModel`: every validation message, `Test` success and failure text; `ConnectionListViewModel`: delete confirmation), the connection manager and editor windows of both heads (`ConnectionEditorWindow.axaml(.cs)`, `ConnectionEditorWindow.xaml(.cs)`, connection list / manager views, folder and environment labels, SSH section labels, the SQLite browse button and file-picker title and filter names).
- Validation messages are produced by the view-model (not by the head) through `UiText`, so both heads show the same text; remove any head-side copy of a validation message (the French messages added in the WinUI code-behind by the fix commit move to the view-model or the catalog, keeping exactly their French wording).
- Environment names (`Development`/`Staging`/`Production` or the existing `ConnectionEnvironment` values) are displayed through catalog keys, not `ToString()` of the enum.
- The result of "Test connection": translated heading, then the raw driver message unchanged (recipe rule 5).
- File-picker file-type names ("SQLite database", "All files") are catalog entries.

## Out of scope
- Dialog buttons shared by all dialogs (OK/Cancel in the dialog services: P10-16), explorer connection node text (P10-11).

## Tasks
- [x] Migrate every `connections` inventory line and tick it.
- [x] Tests per recipe rule 7, including: each validation message in French and English for an invalid profile; a SQLite profile without a file; a test that a driver exception message is passed through unchanged.
- [ ] English captures of the editor (every tab/section) and the list, both heads. NOT DONE: the windows cannot be driven from the agent session; to check by hand with `SQUILL_LANGUAGE=en` (see `tools/visual/README.md`).

## Acceptance criteria
- Common criteria of the recipe. No English validation message appears in the French UI and no French one in the English UI (the report quotes the check).

## Suggested commit message
Localize the connection editor, validation and connection list
