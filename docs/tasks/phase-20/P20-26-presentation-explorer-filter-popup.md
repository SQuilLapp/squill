# P20-26 - Presentation: the explorer filter popup view-model

Status: done
Depends on: P20-25

## Goal
One popup view-model, opened from a node, that edits what the explorer shows (owner screenshots 2 and 3, DataGrip style): from a **connection** it lists the databases (and, expanded, the schemas of an opened database); from a **database** it lists its schemas (and, expanded, the tables of a schema); from a **schema** or a **Tables folder** it lists the tables and views of that schema. Each level has an "All ..." row with "add pattern", a "Default ..." row when the level has a default, one check box row per object, and the patterns. Changes apply at once (no OK / Cancel). The heads draw it in P20-27 / P20-28 from the flat row list below.

Visual reference (owner screenshot 3, dark):
```
                                          [refresh] [expand all] [collapse all]
 >  [ ] All databases   add pattern
 >  [ ] Default database (postgres)
 >  [ ] azure_maintenance            (bold)
 >  [x] gate
 v  [x] sodda_web                    (row selected)
      [ ] All schemas   add pattern
      [ ] Default schema (public)
      [x] public  (Default schema)
      [x] ticker
 >  [x] sodda_worker
```
(System schemas such as `information_schema` / `pg_catalog`, with a lightning mark in DataGrip, are not read by SQuilL's schema readers: they never appear.)

## Design decisions (final)
- **Types** in `src/Squill.Presentation/Explorer/ExplorerFilterViewModel.cs` (and `FilterRow.cs`), XML doc on everything public:
  - `public enum FilterRowKind { All, Default, Pattern, Item }`.
  - `public sealed partial class FilterRow : ViewModelBase`: `Kind`; `Text` (name, or the localized "All databases" / "Default schema" text, or the pattern as typed); `Note` (`string?`, drawn dimmer after the text: `(postgres)` on a Default row, `(Default database)` on the default item); `Depth` (0 or 1, the heads indent by `Depth * 20` px); `IsStrong` (true for item rows at depth 0 when the popup has two levels: database names are bold in the screenshot); `[ObservableProperty] bool IsChecked` (read by the heads; **set only by the view-model**); `CanExpand`; `[ObservableProperty] bool IsExpanded`; `CanAddPattern` (All rows); `[ObservableProperty] bool IsAddingPattern`; `[ObservableProperty] string PatternText`; commands `ToggleCommand`, `ToggleExpandCommand`, `StartPatternCommand`, `CommitPatternCommand` (Enter), `CancelPatternCommand` (Escape), `RemovePatternCommand` (Pattern rows, drawn with an `x` button and no check box).
  - `public sealed partial class ExplorerFilterViewModel : ViewModelBase`: `ObservableCollection<FilterRow> Rows` (flat, in display order, only rows under expanded parents and passing the search); `[ObservableProperty] string SearchText` (placeholder `explorer.filter`, as in the old dialog: keeps item rows whose text contains it, ignoring case, with their parent, which is expanded; All / Default / Pattern rows always stay); `RefreshCommand` (async), `ExpandAllCommand`, `CollapseAllCommand`.
  - Created by `public ExplorerFilterViewModel? CreateFilter(ExplorerNode? node)` on `ExplorerTreeViewModel`: connection with a session -> databases popup; opened database -> schemas popup (tables popup when the snapshot has no named schema); schema or a `Folder` whose title is `explorer.tables` under a schema or database -> tables popup of that schema; anything else, or no session -> `null`. The heads show nothing for `null`. `public bool CanFilter(ExplorerNode? node)` answers the same question without building the view-model (the heads use it to show the context menu entry).
- **Levels** come from P20-25 (`DatabaseLevel`, `SchemaLevel`, `TableLevel`); after every change the view-model calls `ExplorerTreeViewModel.ApplyVisibility(profileId)` and then updates `IsChecked` of the existing rows **in place** (no `Rows` rebuild on a toggle, so the heads keep focus and scroll). `Rows` is rebuilt only on expand, collapse, search, pattern added or removed, and refresh; expanded state is kept across rebuilds (by level and name).
- **Rows per level** (one level = an "All" row, optional "Default" row, the pattern rows, then the items sorted like the explorer):
  - databases: items = the database names of P20-25, sorted `OrdinalIgnoreCase` as in the screenshot (the default database is not moved first); default = the profile's database (`ConnectionProfile.Database`), Default row text `explorer.filter.defaultDatabase` with note `(name)`, the item gets note `(Default database)` (`explorer.filter.defaultDatabaseNote`); an item `CanExpand` only when its session is open (`session.FindDatabase(name)` or the root's own database) and then its children are that database's schema level (or tables level without schemas). Unopened databases have no chevron: the popup never opens a connection.
  - schemas: items = schema names of the snapshot; default = `SchemaSnapshot.DefaultSchema` when present among them (texts `explorer.filter.defaultSchema`, note `(Default schema)` via `explorer.filter.defaultSchemaNote`); in a schemas popup each schema `CanExpand` to its tables.
  - tables: items = table and view names of one schema (displayed without the `schema.` prefix; keys keep it); no Default row; patterns typed here are stored with the `schema.` prefix and displayed without it; the All row uses `ShowAll(keys, "schema.")` / `HideAll(keys)`.
  - Two levels at most: the node's level and the one under it.
- **Toggle rules**: Item -> `Hide` when visible, else `Show`. All -> checked when `AllVisible(items)` and no pattern in scope; toggling a checked All calls `HideAll(items)`, an unchecked one `ShowAll(items[, prefix])`. Default -> mirrors and toggles the default item (no separate saved flag). Pattern add: `AddPattern(PatternText)`; a refused pattern (blank, duplicate) keeps the box open and does nothing else.
- **Refresh**: `RefreshSchemaAsync` of the sessions in scope (connection: the root and its `OpenedDatabases`; database / schema: that session), `ExplorerTreeViewModel.Reload` for each, then rebuild `Rows`. The list of database names is not re-read (`ConnectionSession.Databases` is read once when the session opens).
- **Catalog keys** (fr and en, parity test): `explorer.filter.allDatabases` (Toutes les bases / All databases), `explorer.filter.allSchemas` (Tous les schémas / All schemas), `explorer.filter.allTables` (Toutes les tables / All tables), `explorer.filter.defaultDatabase` (Base par défaut / Default database), `explorer.filter.defaultSchema` (Schéma par défaut / Default schema), `explorer.filter.defaultDatabaseNote` (`(base par défaut)` / `(Default database)`), `explorer.filter.defaultSchemaNote` (`(schéma par défaut)` / `(Default schema)`), `explorer.filter.note` (`({0})`, both), `explorer.filter.addPattern` (ajouter un motif / add pattern), `explorer.filter.patternHint` (`Motif, * et ? acceptés` / `Pattern, * and ? allowed`), `explorer.filter.removePattern` (Retirer le motif / Remove pattern), `explorer.filter.expandAll` (Tout déplier / Expand all), `explorer.filter.collapseAll` (Tout replier / Collapse all), `explorer.chooseShown` (`Choisir ce qui est affiché...` / `Choose what to show...`, the context menu entry of P20-27 / P20-28). Refresh reuses `explorer.refresh`; the search placeholder reuses `explorer.filter`.

## Out of scope
- Drawing the popup and the badge click (P20-27, P20-28). Removing `ChooseTablesCommand`, `ITableVisibilityPicker` and the old dialog keys (P20-28). Opening a database from the popup. System schemas.

## Tasks
- [x] Tests first, `tests/Squill.Presentation.Tests/Explorer/ExplorerFilterViewModelTests.cs` (fixtures of `ExplorerVisibilityTests` from P20-25):
  - `CreateFilter`: connection -> first rows `All databases`, `Default database` (note `(d)`), then the sorted databases with `(Default database)` on `d`; database with schemas -> `All schemas`; schema -> `All tables` and table names without prefix; column node and disconnected connection -> `null`.
  - toggling an item hides the node in `Roots` and saves; `Rows` is the same instance list (no rebuild) and the All row becomes unchecked; All toggles hide then show everything; Default mirrors the default item both ways.
  - expanding an opened database adds its schema rows at depth 1; an unopened one has `CanExpand == false`; `ExpandAll` / `CollapseAll`.
  - pattern: `sodda_*` on the databases All row -> only matching databases shown, a Pattern row appears, `RemovePatternCommand` restores; blank and duplicate refused; a table pattern typed in schema `public` is stored as `public.<pattern>` and displayed without prefix.
  - search keeps a parent of a matching child, expanded.
  - fr and en texts of the All / Default rows and notes.
- [x] Run, see them fail; implement; catalog keys and parity.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) (`Squill.slnx`; heads untouched but `Squill.Windows.slnx` must still build).

## Risks
- A toggle that triggers `ApplyVisibility` rebuilds explorer nodes: the popup must keep no `ExplorerNode` reference across calls (work from the session, profile id and names only).

## Suggested commit message
Add the explorer filter popup view-model
