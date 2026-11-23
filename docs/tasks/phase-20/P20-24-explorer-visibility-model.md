# P20-24 - Platform and Presentation: what the explorer shows, per level, with wildcard patterns

Status: todo
Depends on: none

## Goal
Owner request (2026-11-23): replace the "Tables to show" dialog (`explorer.tablesDialog`, `WinUiTablePicker` / `AvaloniaTablePicker`) with a DataGrip-style filter popup that chooses, from the node it is opened on, which **databases** of a connection, which **schemas** of a database and which **tables** of a schema the explorer shows, with `n of m` badges on the nodes and wildcard patterns ("add pattern"). Owner screenshots: connection node with badges (`postgres@host  3 of 6`, `sodda_web  2 of 4`), the popup filtering databases, the popup with a database expanded to its schemas, and the current table dialog.

This card is the pure foundation: the saved format (backward compatible with the files written today) and the per-level visibility rules with patterns. No tree, no popup, no head.

Today (read before starting):
- `src/Squill.Platform/Storage/TableFilter.cs`: `record TableFilter(IReadOnlyDictionary<string, IReadOnlyList<string>> Hidden)`: hidden table keys (`schema.name`, or `name` without a schema) per database name; one JSON file per profile in `table-filters/<id:N>.json` (`JsonTableFilterStore`, `ITableFilterStore`, `StorageJsonContext`). Hiding is **opt-out**: a table created later is shown.
- `src/Squill.Presentation/Explorer/ExplorerTreeViewModel.cs` keeps `Dictionary<Guid, Dictionary<string, HashSet<string>>> _hidden` and compares names with `StringComparer.OrdinalIgnoreCase`.

## Design decisions (final)
- **Saved format** (`src/Squill.Platform/Storage/TableFilter.cs`, same file and record name, so `ITableFilterStore` and the store do not change shape):
  - New `public sealed record NameFilter(IReadOnlyList<string> Hidden, IReadOnlyList<string> Patterns)` in `src/Squill.Platform/Storage/NameFilter.cs` (XML doc: names hidden one by one, and the wildcard patterns that, when there is at least one, restrict what is shown).
  - `TableFilter` keeps `Hidden` (tables) and gains three init-only properties, all defaulting to empty so an old file reads as "nothing else filtered": `NameFilter? Databases { get; init; }` (databases of the connection), `IReadOnlyDictionary<string, NameFilter> Schemas { get; init; } = empty` (schemas per database name), `IReadOnlyDictionary<string, IReadOnlyList<string>> TablePatterns { get; init; } = empty` (table key patterns per database name, matched against `schema.name` keys). Update the record's `<summary>` (it is no longer tables only).
  - `JsonTableFilterStore.LoadAsync` re-wraps **every** dictionary with `StringComparer.OrdinalIgnoreCase` (it does it for `Hidden` today) and turns a `null` read from an old file into empty. Add `[JsonSerializable(typeof(NameFilter))]` to `StorageJsonContext` only if the generator does not reach it through `TableFilter`.
- **Wildcards** (`src/Squill.Presentation/Explorer/WildcardPattern.cs`, `public static class WildcardPattern`): `public static bool IsMatch(string pattern, string name)`: `*` any run of characters (empty included), `?` exactly one character, every other character literal, case-insensitive (`OrdinalIgnoreCase` per character through `char.ToUpperInvariant`), whole name. No `Regex` (no escaping questions). `public static bool HasWildcard(string pattern)`.
- **One level** (`src/Squill.Presentation/Explorer/NameVisibility.cs`, `public sealed class NameVisibility`): the mutable state of one level (the databases of a connection, the schemas of one database, or the tables of one database). Names compare with `OrdinalIgnoreCase`.
  - `NameVisibility(IEnumerable<string>? hidden = null, IEnumerable<string>? patterns = null)`.
  - Rule: `IsVisible(name)` = not in `Hidden`, and (no pattern, or at least one pattern matches). So with no pattern the level is opt-out like today (new objects are shown); adding a first pattern switches the level to "only what matches" (the DataGrip meaning of a pattern). An exact name is a pattern without wildcard.
  - `bool ShowsEverything` = no hidden name and no pattern.
  - `void Show(string name)`: removes it from `Hidden`; if it is still not visible (patterns exist, none matches), adds the exact name to `Patterns`.
  - `void Hide(string name)`: removes an exact (no wildcard) pattern equal to `name`; if the name is still visible, adds it to `Hidden`.
  - `bool AllVisible(IEnumerable<string> names)`: every name is visible.
  - `void HideAll(IEnumerable<string> names)`: `Hide` each.
  - `void ShowAll(IEnumerable<string> names, string? scopePrefix = null)`: removes the names from `Hidden` and removes the patterns in scope (all of them when `scopePrefix` is `null`; else those that start with `scopePrefix`, ignoring case); if patterns remain afterwards, adds the pattern `scopePrefix + "*"` (only when `scopePrefix` is not `null`) so the scope stays fully shown. `scopePrefix` is used for the tables of one schema inside the per-database tables level (`"public."`).
  - `bool AddPattern(string pattern)`: trims; refuses (returns `false`) empty text and a duplicate (ignoring case); else adds it.
  - `void RemovePattern(string pattern)`.
  - `IReadOnlyCollection<string> Hidden`, `IReadOnlyList<string> Patterns` (in the order added), `NameFilter? ToSaved()` (`null` when `ShowsEverything`; hidden names sorted with `OrdinalIgnoreCase`).
  - `event EventHandler? Changed` is **not** added: the explorer applies and saves after each call (P20-25).

## Out of scope
- The explorer tree, the badges (P20-25), the popup view-model (P20-26), the heads (P20-27, P20-28). PostgreSQL system schemas (`pg_catalog`, `information_schema`) are not read by `PostgresSchemaReader` (`where n.nspname not in ('pg_catalog', 'information_schema', 'pg_toast')`), so they cannot be offered: no "system" marker anywhere.

## Tasks
- [ ] Tests first:
  - `tests/Squill.Platform.Tests/JsonTableFilterStoreTests.cs`: round trip of a `TableFilter` with all four parts; an old file (only `{"Hidden":{...}}`, write the JSON text by hand) loads with empty `Schemas` / `TablePatterns` and `Databases` `null`; lookups in every loaded dictionary ignore case.
  - `tests/Squill.Presentation.Tests/Explorer/WildcardPatternTests.cs`: `sodda_*` matches `sodda_web` and `SODDA_WORKER`, not `gate`; `*` matches empty and anything; `a?c` matches `abc` not `ac` / `abbc`; `public.tmp_*` matches `public.tmp_x` not `ventes.tmp_x`; literal dot and brackets; trailing `*` after a literal; `**`.
  - `tests/Squill.Presentation.Tests/Explorer/NameVisibilityTests.cs`: opt-out by default (a new name is visible); `Hide` then `Show` round trip; first `AddPattern` hides the non-matching names; `Show` of a non-matching name in pattern mode adds an exact pattern and `Hide` removes it again; `HideAll` / `AllVisible`; `ShowAll` without prefix resets to `ShowsEverything`; `ShowAll` with prefix `public.` removes only `public.` patterns and adds `public.*` when `ventes.*` remains; `AddPattern` refuses blank and duplicates; `ToSaved` is `null` when everything is shown and sorted otherwise; case-insensitive names.
- [ ] Run, see them fail; implement.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) (`Squill.slnx`; no head code changed, so no AOT publish). `ExplorerTreeViewModelTests` and `JsonTableFilterStoreTests` unchanged and green (the old shape still works).

## Risks
- A missing property in an old JSON file leaves the init default only if the property has an initializer; check the `null` case anyway (the source-generated context may write `null`).

## Suggested commit message
Saved explorer visibility per level with wildcard patterns
