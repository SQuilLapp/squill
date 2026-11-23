# P20-25 - Presentation: the explorer hides databases, schemas and tables, and shows `n of m` badges

Status: todo
Depends on: P20-24

## Goal
Move `ExplorerTreeViewModel` from "hidden tables per database" to the three levels of P20-24 (databases of a connection, schemas of a database, tables of a database), apply them when the tree is built, save them per connection, and give connection, database and schema nodes the DataGrip `n of m` badge (screenshot: `postgres@host  3 of 6`, `gate  1 of 3`, `sodda_web  2 of 4`). The popup that edits the levels is P20-26; this card only adds the state, the rules and the methods the popup will call.

Today (`src/Squill.Presentation/Explorer/ExplorerTreeViewModel.cs`): `_hidden` (`Dictionary<Guid, Dictionary<string, HashSet<string>>>`, profile id -> database -> table keys), `HiddenOf`, `SetHidden` (reloads and saves through `ITableFilterStore`), `LoadFilter` / `LoadFilterAsync` (read once when the first session of a connection opens), `Populate` (adds the profile's database node, then one node per other name in `session.Databases`), `PopulateDatabase` (returns the number of hidden tables; schema nodes come from the tables and routines of the snapshot), `FillDatabase` (sets `HiddenCount` and `Detail = explorer.hiddenTables`), commands `ChooseTablesAsync` (opens `ITableVisibilityPicker`), `HideTable`, `ShowAllTables`. `ExplorerNode.Trailing` = `ErrorText ?? Detail ?? Count`.

## Design decisions (final)
- **State**: replace `_hidden` with `Dictionary<Guid, ConnectionVisibility> _visibility`, where `ConnectionVisibility` is a private (or `internal sealed`, one-line `<summary>`) class holding `NameVisibility Databases`, `Dictionary<string, NameVisibility> Schemas` and `Dictionary<string, NameVisibility> Tables` (both keyed by database name, `OrdinalIgnoreCase`, created on first use). Table names in the tables level are the existing keys (`KeyOf`: `schema.name`, or `name`). Loading maps `TableFilter.Hidden` + `TablePatterns` to `Tables`, `Schemas` to `Schemas`, `Databases` to `Databases`; saving builds a `TableFilter` back (only non-empty levels, through `NameVisibility.ToSaved()`). Keep the "read once per connection, rebuild when it hides something" behavior and the IO-error swallow of `SaveFilterAsync`.
- **Applying**:
  - `Populate`: the database nodes are the profile's database (named as today) and the other names of `session.Databases`; a name the databases level hides gets no node. The total `m` is the number of database nodes before filtering.
  - `PopulateDatabase`: a schema (non-empty `Schema` key) the schemas level of that database hides gets no node, nor its tables, views, routines and triggers; tables and views go through the tables level (today's `hidden.Contains(KeyOf(t))` becomes `!tables.IsVisible(KeyOf(t))`). SQLite (empty schema key) has no schema level: its tables sit under the database node as today.
  - `HiddenCount` keeps its meaning (tables and views of the database the tables level hides; the heads use it for "Show all tables"). `Detail = explorer.hiddenTables` is **removed** (the badge says it now); remove the keys `explorer.hiddenTables.one|.other` from both catalogs.
- **Badges** (`src/Squill.Presentation/Explorer/ExplorerNode.cs`): `[ObservableProperty] public partial string? FilterBadge { get; set; }` with `[NotifyPropertyChangedFor(nameof(Trailing))]` and `[NotifyPropertyChangedFor(nameof(HasFilterBadge))]`, and `public bool HasFilterBadge => FilterBadge is not null`. Text `UiText.Format("explorer.filterBadge", DisplayFormat.Count(n), DisplayFormat.Count(m))`, catalog `explorer.filterBadge` = `{0} of {1}` (en) / `{0} sur {1}` (fr). Set on:
  - a **connection** node with a session, when it has at least two database nodes before filtering (`m` = database nodes, `n` = shown);
  - an **opened database** node: when the snapshot has named schemas, `m` = schemas of the snapshot (as listed today: from tables and routines), `n` = shown; without named schemas (SQLite), `m` = tables and views, `n` = shown; `null` when `m == 0`;
  - a **schema** node: `m` = tables and views of that schema, `n` = shown; `null` when `m == 0`.
  - `Trailing` becomes `ErrorText ?? Detail ?? (FilterBadge is null && Count is { } n ? DisplayFormat.Count(n) : null)`: a schema row shows its badge instead of its object count; folder rows keep their count. The heads draw `FilterBadge` in its own chip (P20-27, P20-28).
- **Methods the popup will use** (public, XML doc), so P20-26 contains no tree logic:
  - `public NameVisibility? DatabaseLevel(Guid profileId)`, `public NameVisibility? SchemaLevel(ConnectionSession session)`, `public NameVisibility? TableLevel(ConnectionSession session)`: the live level objects (created on demand; `null` only for an unknown profile).
  - `public void ApplyVisibility(Guid profileId)`: rebuilds the tree of that connection (`Populate` of the connection node, then `ApplyFilter`, then `SetSelectedNodes(SelectedNodes)` so a hidden node leaves the selection) and saves. The popup calls it after every change (live apply, no OK button).
- **Existing commands** keep working on the new state: `HideTable` -> `TableLevel(session).Hide(KeyOf(table))`; `ShowAllTables` -> `TableLevel(session).ShowAll(all keys)`; `ChooseTablesAsync` (still used by the heads until P20-27 / P20-28) builds its `TableChoice` list from `TableLevel(session).IsVisible`, and on OK calls `HideAll` of the unchecked and `Show` of the checked. Remove `HiddenOf` / `SetHidden` once unused.

## Out of scope
- The popup view-model and its catalog keys (P20-26), the heads (P20-27, P20-28), removing `ITableVisibilityPicker` (P20-28). Listing system schemas (not read by the schema readers). Hiding the connection node itself.

## Tasks
- [ ] Tests first, `tests/Squill.Presentation.Tests/Explorer/ExplorerVisibilityTests.cs` (reuse the fixtures of `ExplorerTreeViewModelTests.cs`: `Session(...)` with `Databases = [...]` and `DatabaseOpener` as at line 365, `MemoryFilters`):
  - databases: connection with `d` + `other` + `bad`: badge `3 of 3`; `DatabaseLevel(id).Hide("other")` + `ApplyVisibility` -> no `other` node, badge `2 of 3`, saved `Databases.Hidden == ["other"]`; a pattern `o*` shows `other` only (the profile database too only if it matches);
  - schemas: snapshot with `public` and `ventes` -> database badge `2 of 2`; hide `ventes` -> its tables, routines and triggers are gone, badge `1 of 2`, saved under `Schemas["d"]`;
  - tables: schema badge `n of m`, `HiddenCount`, schema `Trailing` is the badge not the count; SQLite-like snapshot (no schema) -> the database badge counts tables;
  - a saved file with all three parts is applied when the connection opens (`WaitAsync` as in `SavedFilter_IsAppliedWhenTheConnectionOpens`);
  - a hidden selected table leaves `SelectedNodes`.
- [ ] Update the existing tests that assert `explorer.hiddenTables` (`ChooseTables_HidesTheUnchecked_ShowsACount_AndSavesPerDatabase`, `HideTable_ThenShowAll_RoundTrips`: the database `Trailing` is now `null`, the schema `public` carries the badge) and `Trailing_PrefersDetail_ThenFormattedCount_ThenNull` (add: a badge hides the count). Do not weaken the other assertions.
- [ ] Implement; catalog keys `explorer.filterBadge` (fr, en), remove `explorer.hiddenTables.*`; parity and used-keys tests green.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md). Both heads still build (`Squill.Windows.slnx` too): they bind `Trailing`, `HiddenCount` and `ChooseTablesCommand`, which keep their names.

## Risks
- `Populate` is also reached from `Reload` when the profile's own database is hidden (no child has that session): it must not loop or throw.
- Rebuilding the connection node on every toggle loses the expanded state of schema nodes: keep it (remember the expanded schema titles per database before `Populate` and restore them), as a click in the popup must not collapse what the user opened.

## Suggested commit message
Hide databases, schemas and tables in the explorer, with n of m badges
