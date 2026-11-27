# P21-23 - Presentation: go to definition and find usages commands

Status: done
Depends on: P21-22

## Goal
`F12` / `Ctrl+click` goes to the definition of the name under the caret; `Shift+F12` / `Alt+F7` highlights and lists its usages.

Exists today: `ISqlEditor` (text, caret, selection, diagnostics, completion and hover providers, focus); the explorer can select a node; `WorkspaceViewModel` opens the designer and definition scripts (`DefinitionScript`).

## Design decisions (final)
- `QueryTabViewModel.GoToDefinitionCommand`: definition in the script -> move the caret and select the name; a table/view in the schema -> select it in the explorer and open its properties panel (and with `Ctrl+Shift+B`, open it in the designer); a routine -> open its definition script (existing path). Nothing found -> status text `editor.definition.none`.
- `FindUsagesCommand`: `ISqlEditor` gains `void SetHighlights(IReadOnlyList<TextRange> ranges)` (default implementation empty so heads compile until P21-24/25) and the tab shows a `Usages` list (line, column, line text) in the results area as a message-kind result; `Esc` clears the highlights.
- Palette actions (`PaletteActionRegistry`) for both commands, shortcuts shown only once both heads bind them.
- Keys in both catalogs.

## Out of scope
- Head bindings (P21-24, P21-25).

## Tasks
- [x] Tests with `StubSqlEditor`: caret moves, explorer selection requested, usages result content, highlights cleared.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A definition in another statement of the script: the caret move must scroll the editor (heads).

## Open points (defaults)
- None.

## Suggested commit message
Add go to definition and find usages commands to the query tab

## Done notes
- Premise checked: `ISqlEditor` (text, caret, selection, hover/completion providers, focus), `DefinitionScript`, the explorer selection and `PaletteActionRegistry` exist. Deviations: there is no properties panel (the explorer only exposes `ExplorerNode.DefinitionText` for one), so go to definition selects the node and expands its parents (`ExplorerTreeViewModel.RevealTable`), nothing opens; `SqlSymbol` has no routine kind (P21-22), so routines are not resolved and a routine name gives the `editor.definition.none` status; the results area is the tab's `Results` collection, so the usages are one message result (`line:column  text`, one per line) rather than a list control.
- `QueryTabViewModel` (new file `QueryTabViewModel.Navigation.cs`): `GoToDefinitionCommand`, `GoToDefinitionInDesignerCommand` (the `Ctrl+Shift+B` variant), `FindUsagesCommand`, `ClearHighlightsCommand` (Esc) and the event `TableDefinitionRequested(TableInfo, bool inDesigner)`; `WorkspaceViewModel.AddTab` turns it into an explorer reveal or `DesignTableOn`. A column of a schema table selects its table. In-script definitions (alias, CTE, derived column) move the caret and select the declaration.
- `ISqlEditor.SetHighlights` has a default empty implementation; `StubSqlEditor.Highlights` records it. Palette actions `query.goToDefinition`, `query.goToDefinitionInDesigner`, `query.findUsages` without shortcut (heads bind them in P21-24/25). Keys `editor.definition.none`, `editor.usages.*`, `palette.action.query.*` in both catalogs.
- Tests: `QueryTabNavigationTests` (alias caret and selection, table and designer requests, column, unknown name, usages list and highlights, Esc clears, no symbol), `WorkspaceDesignerTests.GoToDefinition_...` (explorer selection, designer opened).
- Verification: both solutions build with 0 warnings; `dotnet test --solution Squill.slnx`: every project green except one random `Squill.App.Avalonia.Tests` UI test per run (thread-affinity exception, also on the stashed baseline; recorded in P21-113). No AOT publish (Presentation only).
