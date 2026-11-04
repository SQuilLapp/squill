# P11-20 - Presentation: routines, triggers and "open definition" in the explorer

Status: done
Depends on: P11-15

## Goal
The explorer tree shows Routines (functions and procedures) and Triggers folders under a schema, and views/routines/triggers can be opened as a script in a new query tab.

## Design decisions (final)
- New `ExplorerNodeKind` members `Function`, `Procedure`, `Trigger`, `MaterializedView`; folders "Routines" and "Triggers" appear only when non-empty (counts like P9-27; SQLite has no Routines folder, an empty folder is never shown).
- Action "Open definition" (`ExplorerNode` command, same plumbing as "open table"): opens a new query tab on the connection containing `Definition` (view: `CREATE VIEW <qualified name> AS` + definition when the dialect stores only the body (MySQL, SQL Server stores the full text, PostgreSQL returns the body, SQLite the full statement); routine and trigger text as stored, MySQL routine wrapped with `DELIMITER`-free `CREATE FUNCTION|PROCEDURE name(params) body` built from `Signature`). The text goes through `UiText` only for comments; SQL text is never translated. Put the per-dialect wrapping in one small static class `DefinitionScript` in `Squill.Presentation/Explorer` with unit tests for the 4 dialects x (view, routine, trigger) built from literal `TableInfo`/`RoutineInfo`.
- "Script as CREATE TABLE" for tables: uses `DdlGenerators.For(dialect).CreateTable` from the table's design (the designer already converts `TableInfo` to a design; reuse that converter). Same command, same new-tab mechanism.
- Properties panel shows the definition text for the selected node when present (read-only; look at what the panel binds today).
- Catalog keys fr/en for folder names and command names; both heads get only the glyph and menu wiring in P11-21 and P11-22.

## Out of scope
- Editing or running the definition automatically (the tab only opens; the user presses Run).

## Tasks
- [x] Node kinds, tree building from the snapshot, `DefinitionScript`, command, tests incl. ordering and counts; localization guards green.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Show routines and triggers in the explorer and open object definitions
