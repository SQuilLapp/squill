# ADR 0017 - Verification passes and multi-table delete (phase 20)

- Status: accepted
- Date: 2026-11-20

## Context
Phase 19 ([ADR 0016](0016-debug-and-polish.md)) was merged with three partial cards and many screens never driven, because no agent could click the windows. Phase 20 ([task index](../tasks/README.md), cards P20-01 to P20-23, shared decisions in [verification-recipe.md](../tasks/phase-20/verification-recipe.md)) built the missing driver, ran the passes it allowed, fixed what they found, and added one small feature from `docs/ideas.md`: drop several tables at once from the explorer.

## Decision

### Window driver (P20-01)
`tools/visual/Drive-Window.ps1` (Dump, Click, Key, Type, Wait, Hover, Focus-Dialog, SelfTest) drives a SQuilL window through UI Automation with a mouse or key fallback. **Foreground rule**: keys and mouse input are sent only when the target process owns the foreground window; otherwise the script refuses, so that input never reaches another program. About 14 controls per head have no automation name and are listed in `tools/visual/README.md`.

### WinUI theme lookups (P20-02)
Brushes are read through `Tokens` (element theme), never through `Application.Current.Resources[...]`, which follows the system theme. The test `Token_brushes_are_read_through_Tokens` scans `src/Squill.App.WinUI` and fails on any direct lookup of a `...Brush` or `...Color` key. It fixed `CommandPalette`, `DataGridView`, `SearchBox` and `HistoryView`.

### PostgreSQL scenario (P20-03)
`tools/visual/New-PostgresScenario.ps1` starts a `postgres:17-alpine` container with roles, a view and 450 tables; `New-PostgresSessions.ps1` opens up to 450 sessions with one blocking chain.

### Multi-table drop (P20-04 ... P20-09)
- Tables only (`TableKind.Table`), of **one connection session**, one action. A selection with a view, or tables of two connections, disables the command.
- `WorkspaceViewModel.DropTablesCommand` is the only flow; the single `DropTableCommand` calls it. **This closes a gap**: before phase 20 the single drop ran `session.Connection.ExecuteAsync` directly, so a `DROP TABLE` on a Production connection ran without the production confirmation, outside the session transaction (no journal, no savepoint) and without the MySQL implicit-commit question. It now goes through `ProductionRequestBuilder.ConfirmWriteAsync` (`SchemaChange`) and `SqlRunner` scopes.
- Statements come from `DdlGenerators.For(dialect).DropTable(schema, name)`. **No `CASCADE`, ever.** A table outside the selection that references a selected table blocks the drop with the list, and nothing is sent.
- Order: referencing tables before the tables they reference (`TableDropPlan`, topological over the foreign keys inside the selection); a cycle keeps the selection order and the engine's error is reported.
- Atomicity: PostgreSQL, SQL Server and SQLite run the drop in one scope (own transaction, or a savepoint inside the user's open transaction): all or nothing. MySQL cannot; the flow stops at the first failure and says which tables were dropped and which failed (observed in P20-09: both drops stay dropped).
- Confirmation: outside Production a plain dialog lists the qualified names; on Production only the production dialog (statements, database name typed) is shown.
- Explorer: `SetSelectedNodes`, `SelectedTables`, `DropMenuText` (`Supprimer 2 tables`), `CanDropMenu`, `Delete` key. WinUI `TreeView` with plain `SelectionMode="Multiple"` shows a checkbox on every row, so the explorer uses a copy of the default `TreeViewItem` template (`ExplorerTreeViewItemStyle`, to re-copy when the Windows App SDK is upgraded) and rebuilds the mouse selection (click, Ctrl, Shift) from the button release. Avalonia uses the native multiple selection.
- Proved on real engines: `DropTablesTests` on SQLite, PostgreSQL, MySQL and SQL Server (4 tests each, P20-09). Unit tests: 11 (P20-04), 10 (P20-05), 11 (P20-06), 7 headless UI tests (P20-08).

### Fixes made by the verification passes
- WinUI: `DdlPane` redraw (P20-10); read-only marker under a filter, export menu count and CSV separator label (P20-11, twins on Avalonia in P20-18); stuck single-entry completion popup closed when a run starts (P20-12, and on Avalonia in P20-18); privilege matrix widths (P20-14); light-theme plan details and splitters (P20-15).
- Avalonia: status bar environment follows the compare target (P20-16); `SecurityView` and `SessionsMonitorView` paint an opaque background (P20-17); the transaction toolbar group also hides the run keycaps so `Annuler` stays visible (P20-20).

## Consequences
- A Production `DROP TABLE` now always asks, and an open manual transaction journals it.
- The owner's checklists in `docs/verification/` are still the acceptance; `multi-delete.md` is prepared and unticked.

## Known limits
- **Never run on Linux or macOS.** `Cmd+Backspace` for the drop was not added.
- **Oracle**: `DdlGenerators.UsesTransactionalDdl` treats it as transactional (only MySQL is not); the multi-drop on Oracle was not proved.
- **The Avalonia multi-select was not driven in the published exe** (the driver has no modifier click and a plain click did not select a row): headless tests only. WinUI: the owner drove click, Ctrl+click, Shift+click, the menu text and the refusal dialog; nobody drove `Delete`, the confirmation, cancel or the light theme.
- **Partial cards**: P20-03 and P20-10 ... P20-20 are `partial`. Main gaps: English and light theme on most screens; the WinUI import wizard (M10) past step 1 (native file picker not reachable); the WinUI ER diagram (M08); the Avalonia privilege matrix, apply, session actions and dark theme; Production dialogs of the PostgreSQL screens; plan rows and the `Failed` variant; AltGr chords; narrow window; MySQL and SQL Server screens. The per-card list is in the task index, "Left open by phase 20".
- **Avalonia popups** (menus, flyouts) do not appear in `Screen` or `PrintWindow` captures: the explorer context menu and the `Exporter` menu were not seen.
- **Avalonia password dialog** of a profile without stored password was not driven (P20-03).
- Some fixes have no test (canvas and layout code: `DdlPane`, the WinUI plan panel, `SecurityView` widths, `HideSuggest`). Two Avalonia tests only guard a rule: the compare environment test passes with and without the fix, and the toolbar overflow test asserts "fits or chip hidden".
- **AOT baselines**: the WinUI and Avalonia publishes show only the four third-party warnings of `eng/aot-warnings.baseline.txt`; not re-evaluated against newer package versions.
- Possible WinUI native crash (`0xc0000374`, heap corruption, no `crash.log` entry) after opening the connection edit dialog twice, or right-clicking the explorer right after saving it (P20-14): seen twice, not reproduced, **no card yet**.
- Open bugs carded: [P20-22](../tasks/phase-20/P20-22-winui-designer-cell-text-dim-when-edited.md) (WinUI designer cell text dim once edited), [P20-23](../tasks/phase-20/P20-23-designer-sqlite-fresh-open-reports-changes.md) (an untouched SQLite table already reports changes).
- Reported, not carded: WinUI completion popup cut by the right window edge and no documentation panel; the LoomEditor find widget is English in a French UI; tall completion popup on Avalonia; WinUI title bar search box and Auto-commit / Manuel clipped at 900 and 600 px; PostgreSQL sessions text truncated (WinUI) and `Durée` / `Requête` cut (Avalonia) at 1440 px; Avalonia M09 / M10 differences from the mockups; the Avalonia tab-switch path may skip `LayoutToolbar`.
- Corrections owed to earlier documents, not made here: ADR 0016 and ADR 0002 still say the completion popup is AvaloniaEdit's (it is LoomEditor's `SuggestWidget`); the ADR 0016 "Known limits" lines for P19-09 ... P19-20 are partly superseded by the passes above.

## Not built
- Dropping views, `CASCADE`, selection across connections, undo.
- Cross-engine schema compare ([docs/ideas.md](../ideas.md), "what it would need" in the task index).
- Everything in [ADR 0015](0015-remaining-mockups.md) "Not built".
