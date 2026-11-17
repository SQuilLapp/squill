# P17-12 - Presentation: palette objects provider (risky)

Status: done
Depends on: P17-11

## Goal
Search tables, views, materialized views, routines and triggers of every open connection by name, fast and off the UI thread, and open them. Recipe decisions 14 and 15, risk "large results" in spirit (50,000 objects).

## Design decisions (final)
- `src/Squill.Presentation/Palette/PaletteObjectProvider.cs`. Input: the workspace's open sessions (`WorkspaceViewModel.Sessions`, each `ConnectionSession.Schema` as a `SchemaSnapshot`) and the active session. `Search(string term, CancellationToken ct)` returns `ValueTask<IReadOnlyList<PaletteItem>>`, at most 200 items sorted by score (ties: active connection first, then shorter name, then name). It takes an **immutable index** built from the snapshots and rebuilt when a session's `Schema` instance changes (compare the reference per session; sessions opened or closed also invalidate): a flat array of `(string Name, kind, schema, session)` plus the folded lower-case name so the hot loop does not renormalize; building it for 50,000 objects must stay well under one second and happens on a worker the first time the provider is used after a change, never on the UI thread.
- Matching runs on a worker (`Task.Run`), checks `ct` every 1,024 candidates, matches the **name** (schema and kind only break ties), uses `FuzzyMatcher` from P17-10. A stale search (cancelled by the next keystroke) throws `OperationCanceledException` and publishes nothing.
- Row data: `Title` = the object name, `Detail` = kind caption and schema as in M13 (`table · ventes.public`, `vue · ventes.analytics`; kinds through `UiText` keys `palette.kind.table`, `.view`, `.materializedView`, `.function`, `.procedure`, `.trigger`; for SQLite or MySQL with no schema the database name), `Right` = the connection name, `Environment` = the profile's environment, `IconKey` per kind, `Ranges` from the match.
- `Run(Default)`: tables, views and materialized views call the same code the explorer double click runs (`WorkspaceViewModel.OpenTableAsync` with the matching `TableInfo`; for another connection the workspace first makes that session the active one, the same way the explorer does when a node of another connection is used: read `ExplorerTreeViewModel`); functions, procedures and triggers open their definition through `DefinitionScript` the way the explorer does. `Run(NewTab)`: for tables, views and materialized views a new query tab on that session with the text `SELECT * FROM <quoted qualified name>` (quoting through `Squill.Sql` identifier quoting of the dialect, never hand-quoted) and the caret at the end, not executed; for the other kinds the same as `Default`. If the explorer already has a "select all rows" style command, reuse it and say so.
- A table hidden by the "Choose tables" filter (`ITableFilterStore`, the explorer's own visibility) **still appears** in the palette: the palette searches the schema snapshot, not the explorer tree; this is a decision, note it in the ADR (P17-18).
- Nothing connected: `Search` returns an empty list and the view-model (P17-14) shows the hint `Connectez-vous pour rechercher des objets`.

## Out of scope
- History and line (P17-13), the merged view-model (P17-14), searching column names, objects of connections that are not open.

## Tasks
- [x] Provider, index, run behavior, catalog keys (`palette.kind.*`, `palette.objects.hint`).
- [x] Tests `tests/Squill.Presentation.Tests/Palette/PaletteObjectProviderTests.cs` with fake sessions and a built `SchemaSnapshot`: ranking and the 200 cap, active connection first, several connections with the same table name give two rows with their connection names, schema detail text, `Run` opens the table through the workspace (fake) for a table, a view, a procedure, `Run(NewTab)` produces the quoted `SELECT *` text per dialect (SQLite, PostgreSQL, MySQL, SQL Server quoting), cancellation of the first of two searches publishes nothing, an index rebuild happens after `RefreshSchemaAsync` replaced the snapshot, 50,000 objects searched under a generous bound, hidden-by-filter tables are found.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched.
- Stop rule: if opening an object of a non-active connection cannot reuse the explorer's path (it is private to the tree view-model), expose the minimal shared method in this card and say so; do not duplicate the activation logic.

## Suggested commit message
Add the palette objects provider

## Implementation notes
- The provider talks to the workspace through the new `IPaletteObjectHost` (implemented explicitly by `WorkspaceViewModel`): `OpenTableAsync` makes the session active, then runs the explorer's `OpenTableOnAsync`; `OpenScript` makes it active and opens a tab with the caret at the end. No explorer logic was duplicated; the explorer's private `OpenDefinition` only builds `DefinitionScript` text, which the provider reuses directly.
- `Run(NewTab)` on a table builds `SELECT * FROM <SqlQuoting.QualifiedName>`; the explorer has no equivalent command.
- `palette.kind.table` joined the same-in-both-languages allow list of `CatalogTests`.
