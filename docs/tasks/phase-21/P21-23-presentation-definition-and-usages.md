# P21-23 - Presentation: go to definition and find usages commands

Status: todo
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
- [ ] Tests with `StubSqlEditor`: caret moves, explorer selection requested, usages result content, highlights cleared.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A definition in another statement of the script: the caret move must scroll the editor (heads).

## Open points (defaults)
- None.

## Suggested commit message
Add go to definition and find usages commands to the query tab
