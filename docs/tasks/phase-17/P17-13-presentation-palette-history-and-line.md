# P17-13 - Presentation: palette history and go-to-line providers

Status: todo
Depends on: P17-11

## Goal
The `!` history rows and the `:` go-to-line row. Recipe decisions 12 and 14 (history is inserted, never executed).

## Design decisions (final)
- `src/Squill.Presentation/Palette/PaletteHistoryProvider.cs`: reads `IQueryHistoryStore.ListAsync(max: 500, ct)` (oldest first, reverse it), keeps the entries of every profile (the connection name and environment come from the profile list when the profile still exists, otherwise `Right` is empty), matches the term with `FuzzyMatcher` on the SQL text collapsed to one line (whitespace runs to one space, cut at 120 characters for the title), empty term = the most recent entries. Row: `Title` = the collapsed SQL, `Detail` empty, `Right` = relative time and duration in M13's shape `il y a 12 min · 18 ms` (`just now`, minutes, hours, days, then the date; `UiText` keys `palette.history.ago.*` with `FormatPlural`; reuse the history panel's relative-time formatter if one exists in `Presentation` (the heads have `HistoryFormat`: if the logic is head-side only, add a Presentation twin and leave the heads alone, noted in the commit message)); failed entries show the muted word `erreur` (reuse the history panel's key). The list is read once per palette opening (cache in the provider, refreshed by `Reload()` which P17-14 calls on open) so typing never touches the disk.
- `Run(Default)`: append the SQL to the active query tab through `ISqlEditor` (text + `\n\n` + SQL when the tab has text, the SQL alone when it is empty; caret placed at the start of the appended statement), or open a new query tab with it when there is no active tab; **nothing is executed**. `Run(NewTab)`: a new query tab on the active session (the entry's profile when it is connected, otherwise the active one) with the SQL. One undo step for the head's editor is out of this card's reach: say in the report whether the editor abstraction offers an edit that is undoable, otherwise the insert replaces `Text` and the report says so.
- `src/Squill.Presentation/Palette/PaletteLineProvider.cs`: for `:` mode and a numeric term returns one `PaletteItem` (`Title` = `Aller à la ligne {n}` with `{total}` lines in `Detail`, key `palette.line.goto`); the line is clamped to `[1, lineCount]`; `Run` moves `ISqlEditor.CaretOffset` to the start of that line (offset computed from `Text`, `\r\n`, `\n` and `\r` all count as one break) and clears the selection. No active query tab: the row is disabled with `palette.line.noEditor`. A non-numeric term returns the hint item `palette.line.hint` (`Saisissez un numéro de ligne`), disabled.

## Out of scope
- Re-running history, searching other fields of an entry, the merged view-model (P17-14), scrolling the head's editor to the caret beyond what setting `CaretOffset` already does (the head cards check it by hand).

## Tasks
- [ ] Both providers, catalog keys (`palette.history.*`, `palette.line.*`).
- [ ] Tests `tests/Squill.Presentation.Tests/Palette/PaletteHistoryProviderTests.cs`, `PaletteLineProviderTests.cs` with a fake store and a fake `ISqlEditor`: most recent first, matching, multi-line SQL collapsed, relative time texts in French and English, failed entry, append with and without existing text, new tab when there is no active tab, nothing executed (fake connection never receives a statement), line numbers clamped, `\r\n` handling, disabled without an editor, hint for `:abc`, the store is read once per `Reload`.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched.

## Suggested commit message
Add the palette history and go-to-line providers
