# ADR 0014 - Grid filter and command palette

- Status: accepted
- Date: 2026-11-17

## Context
Phases 9 and 16 left two mockup screens out because no feature stood behind them: the M05 filter bar and column menu, and the M13 command palette ([ADR 0006](0006-visual-conformance.md)). Phase 17 ([task index](../tasks/README.md), cards P17-01 to P17-18, shared decisions in [filter-palette-recipe.md](../tasks/phase-17/filter-palette-recipe.md)) builds both on top of the existing view-models, in both heads.

## Decision

### Grid filter (M05)
- **Client-side, over the rows already loaded.** A filter, a search or a sort never re-runs the query and never goes back to the server. While the result streams, the view follows. On a truncated result the footer says so; a filter over the whole table is a `WHERE` in the editor.
- **A view layer.** `FilteredGridDataSource` (`Squill.Presentation/Results`) wraps the display grid and maps a view row to a source row. `ResultViewModel.DisplayGrid` returns the view while a filter, a search or a sort is active and the plain grid otherwise, so both heads keep binding one property. `ResultViewState` holds the state of one result tab; nothing is persisted.
- **Read-only while a view is active.** Editing, adding, deleting and reverting rows are refused (reason `filtered`), and a filter, search or sort cannot start while edits are pending. This keeps row identity safe; filtering rows that carry edits is not built.
- **Typed operators** (`GridFilterOperator`): equals, not equals, contains, starts with, ends with, greater, greater or equal, less, less or equal, is null, is not null, in. A column offers the operators of its type category; values are parsed with `CellValueParser`; text matching ignores case. NULL matches only `IsNull` and an `In` set holding the null entry. Filters combine with AND, and the search box is one more AND condition over the formatted text of every column.
- **Sort** (`GridSort`): one column at a time, stable, NULL first ascending and last descending, typed by column category.
- **Column menu** (`ColumnMenuViewModel`): sort ascending or descending and a value list built by `ColumnValueCounts` (distinct values with counts, NULL as its own row, capped at 500 distinct values).
- **Export is unchanged**: it writes the whole committed result, never the filtered rows.

### Command palette (M13)
- **A window overlay** opened by `Ctrl+K` (`Cmd+K` on macOS) and by a click on the title bar search box, closed by `Esc`, a click outside, or running an item. The view-model is `PaletteViewModel` (`Squill.Presentation/Palette`); both heads only draw it.
- **Modes by prefix**: none is everything, `>` actions, `#` objects, `:` go to line, `!` history. The pills rewrite the prefix.
- **Fuzzy matching** (`FuzzyMatcher`) is a case- and accent-insensitive subsequence match with a score; the matched characters are returned so the heads can color them.
- **Run rules.** An action runs only if it can run. An object opens the table data or the definition, and `Ctrl+Enter` opens a new tab with `SELECT * FROM` for tables and views. A history entry is inserted into the active query tab (or a new one) and **never executed**. `:n` moves the caret. The palette closes before the command runs.
- **Objects** come from the schema snapshot of every open connection (`PaletteObjectProvider`), including tables hidden by "Choose tables"; the active connection ranks first; the search runs off the UI thread and a stale search never replaces a newer one.
- **Actions** are registered in `PaletteActionRegistry`; the heads add the ones only they can run (settings, new connection, update check, about, data folder). Every text is a `UiText` key (`palette.*`).

## Consequences
- Both heads share every rule through the view-models; a head never re-implements matching, ordering, operator availability or texts.
- A filtered grid cannot be edited until the filter is cleared; this is a visible reason, not a silent refusal.
- Searching tens of thousands of objects per keystroke is acceptable only because it runs in the background with cancellation.

## Known limits
- The filter works on the loaded rows only; the footer figure is "filtered of loaded".
- Text comparison in a filter ignores case but not accents (the palette ignores both).
- The value list shows the 500 first distinct values.
- The windows themselves (WinUI first) were not driven by hand: the owner's checklist is `docs/verification/grid-filter-palette.md`, still to be ticked.

## Not done
- Freeze, hide and fit width of columns, the row side panel, block size and infinite scroll, the selected-cell sum.
- Filtering rows with pending edits, export of the filtered rows, a persisted filter, OR groups, regular expressions.
- The `@ Symboles` mode and the `FICHIERS` section of M13 (no SQL file projects), running a history entry, palette customization, a memory of recent actions.
