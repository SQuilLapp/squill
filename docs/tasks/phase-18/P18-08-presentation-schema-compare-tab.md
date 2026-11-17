# P18-08 - Presentation: schema compare view-model and tab (risky)

Status: todo
Depends on: P18-05, P18-06, P18-07, P18-02

## Goal
The M09 screen as a workspace tab: pick source and target, compare, browse differences, read both DDLs, edit and run the script. Recipe 1, 2, 4.

## Design decisions (final)
- `src/Squill.Presentation/Compare/SchemaCompareViewModel.cs`, a new tab kind opened like the designer, ER and plan tabs (read how P7-22 / P13-14 register a tab kind and follow it), title `Comparaison`. Command `WorkspaceViewModel.OpenSchemaCompare(sessionId?)`, palette action and menu item (recipe 2), disabled for Oracle and when fewer than two connected sessions of one dialect exist (tooltip reason). SQLite is allowed: two connected SQLite files compare.
- Source / target pickers list the connected sessions of the same dialect and, for a connection with several databases, the database (`ListDatabasesAsync`); choosing a source restricts the target list to the same dialect. `Swap` command. The mockup's `Schéma` selector inside the chip (`ventes . public`) is the database + default schema; compare covers every schema the snapshot holds.
- `CompareCommand` / `Comparer à nouveau`: reads both snapshots (`ReadSchemaAsync`) in parallel on workers with cancellation, then `SchemaComparer` and builds the lists. State: `Idle | Comparing | Ready | Failed(message code)`, `ComparedAt`, `Duration` (`Comparé il y a 12 s . 1,4 s`).
- Object list: filter chips `Ajoutés n`, `Modifiés n`, `Supprimés n` (toggle), filter box `Filtrer les objets`, groups `TABLES`, `VUES`, `INDEX`, `FONCTIONS` with counts, row = checkbox + kind glyph (`+`, `~`, `-`) + name + summary text from `Details` (`4 changements`, `1 colonne`, `nouvelle`, `définition`, `absente source`), `Tout inclure . 7 sur 8 sélectionnés`. `F7` / `Maj+F7` jump to the next / previous difference line in the DDL pane (commands only; keys bound by the heads). Defaults: Added and Modified included, Removed excluded.
- Detail of the selected object: header `public.commandes MODIFIÉ` + the summary sentence, `SourceText` / `TargetText` = the object's DDL on each side (`CreateTable` of its design, or the view / routine definition), aligned by `LineDiff`; `Source . <connection>` / `Cible . <connection>` captions.
- Migration script: rebuilt by `MigrationScriptBuilder` when the inclusion set changes (debounced, on a worker, stale builds dropped); `IsScriptEdited` once the user types (then a change of selection asks before overwriting: `ScriptEditedPrompt`, or simply keeps the text and shows `Régénérer`; choose the latter, simpler). Commands: `Copy`, `SaveAs` (`.sql`, through the existing file save service), `OpenInEditor` (new query tab on the **target** connection with the script), `Execute`.
- `Execute` ("Exécuter sur la cible (PROD)..."): runs the script on the target session through the existing statement runner, **with** the production confirmation of phase 15 (`ProductionGuard`; typed word because the script is destructive when it contains `DROP`), inside `SessionTransaction` rules (an open manual transaction on the target is respected, the question P15-14 style is not duplicated), the notes of the script (`DropsData`, `MySqlImplicitCommit`...) shown before. After success: result summary and an automatic `Comparer à nouveau`. A failure keeps the script and shows the engine message at the failing statement.
- Environment: the source and target chips carry the environment pill and color stripe; the status bar shows `STAGING -> PRODUCTION`, `8 différences . 7 incluses` and the server versions (exposed as properties, the heads draw them).
- All text `UiText` keys `compare.*` (fr + en).

## Out of scope
- Views (P18-09, P18-10), `Données` tab, comparing different dialects.

## Tasks
- [ ] View-model, tab kind registration, workspace command, palette action, catalog keys.
- [ ] Tests in `tests/Squill.Presentation.Tests/Compare/` with two fake sessions: compare lists and counts, chips filter, inclusion changes rebuild the script (stale build never wins), swap, defaults, execute on a Production target asks the confirmation and runs nothing when refused, execute success refreshes, failure keeps the script, `Dialect mismatch` impossible by construction (target list), cancel while comparing.
- [ ] An engine scenario in `tests/Shared` reused by the SQLite test class (always) and the Docker-gated ones: two databases, compare, execute the script on the target, compare again, no included difference left.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched. Riskiest part: the execute path must never bypass the production confirmation; a test proves it.

## Suggested commit message
Add the schema compare view-model and tab
