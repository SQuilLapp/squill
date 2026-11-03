# P10-14 - Migrate strings: table designer and ER diagram

Status: done
Depends on: P10-03, P10-04, P10-07, P10-08, P10-09

## Goal
The table designer (grids, issues, apply flow, DDL preview captions) and the ER diagram (toolbar, tooltips, export, refresh) are fully bilingual in both heads.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Area `designer-er` of `inventory.md`.
- Files in scope (the inventory is authoritative): `src/Squill.Presentation/Designer/TableDesignerViewModel.cs` (`Title` "New table" / "Design {name}", "Apply changes" warnings, "Apply failed", the "Views depending on this table are not recreated." issue which gets a P10-09-style code `design.viewsNotRecreated` created in this card in the Presentation message code class `PresentationMessageCodes` and rendered with `IssueText`), `Diagram/DiagramTabViewModel.cs` (refresh / export / reset / layout-save failures), `ErSvgExporter` fixed text if any, `TableDesignerView`, `DiagramTabView`, `DesignIssueSubjectConverter`, `ErDiagramCanvas` text (cardinality captions, tooltips), and the data-type, referential-action and key-kind captions (`ReferentialActions` display names, "PK", "FK", "NOT NULL" captions: SQL keywords stay SQL; the descriptive words ("Primary key", "Nullable") are catalog keys).
- `DesignIssue` and `EditIssue` rendering uses `IssueText.Format(code, args, message)`; `DesignIssueSubjectConverter` shows the subject (a column or table name) unchanged.
- DDL preview text, data type names, identifiers are data/SQL: not translated.
- The "Drop table" confirmation lives in `WorkspaceViewModel` and belongs to P10-16.

## Out of scope
- Workspace-level dialogs (P10-16).

## Tasks
- [x] Migrate every `designer-er` inventory line and tick it.
- [x] Tests per recipe rule 7 (title new/existing, each warning and error issue in both languages, apply failure heading with raw engine message).
- [ ] English captures: designer on a new and an existing table with issues, ER diagram with tooltip, both heads. (not done: designer and ER screens could not be driven headless; to check by hand)

## Acceptance criteria
- Common criteria of the recipe.

## Suggested commit message
Localize the table designer and the ER diagram
