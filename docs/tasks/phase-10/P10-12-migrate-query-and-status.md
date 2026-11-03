# P10-12 - Migrate strings: query tab, parameters, status bar and editor diagnostics

Status: todo
Depends on: P10-03, P10-04, P10-07, P10-08, P10-09

## Goal
Running queries, the query toolbar, the parameters prompt, the status bar and the SQL diagnostics / hover shown in the editor are fully bilingual in both heads.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Area `query` of `inventory.md`.
- Files in scope (the inventory is authoritative): `src/Squill.Presentation/Query/QueryTabViewModel.cs` ("Run query" discard confirmation, "N row(s) affected" -> `FormatPlural`, "Command completed", schema refresh failure), `ParameterScanner`/parameter prompt texts, `QueryTabView`, `StatusBar` views and the Presentation status text sources, `ParameterPromptRow`, the editor wrappers' diagnostic and hover rendering in both heads (`AvaloniaSqlEditor`, the LoomEditor integration).
- Editor diagnostics and hover: render `SqlDiagnostic`/`ParseDiagnostic` text with `IssueText.Format(code, args, message)` from P10-09 (never `Message` directly); the status-bar diagnostic counter uses `FormatPlural`.
- SQL keywords, completion items (keywords, functions, types, table and column names), the demo script and snippets text are SQL/data: not translated.
- The messages of an executed statement that come from the engine (errors, `RAISERROR`/`NOTICE` output) are shown verbatim.

## Out of scope
- Results grid and export (P10-13), history panel (P10-14).

## Tasks
- [ ] Migrate every `query` inventory line and tick it.
- [ ] Tests per recipe rule 7 (affected-rows plural in both languages, discard confirmation, a diagnostic rendered in both languages, engine error passed through).
- [ ] English captures: query tab idle / running / error / affected rows, parameters prompt, status bar disconnected and connected, a diagnostic underline hover if drivable, both heads.

## Acceptance criteria
- Common criteria of the recipe.

## Suggested commit message
Localize the query tab, parameters prompt, status bar and diagnostics
