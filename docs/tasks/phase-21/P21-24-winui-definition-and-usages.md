# P21-24 - WinUI: F12, Ctrl+click and usage highlights in the LoomEditor

Status: todo
Depends on: P21-23

## Goal
Bind the P21-23 commands in the WinUI head.

Exists today: `src/Squill.App.WinUI/Editors/LoomSqlEditor.cs` (the `ISqlEditor` over LoomEditor, decorations used for highlighting and squiggles), key handling in `QueryTabView.cs`.

## Design decisions (final)
- `F12`, `Shift+F12`, `Alt+F7`, `Ctrl+Shift+B` handled in the editor key handler; `Ctrl+click` if LoomEditor exposes pointer position to offset (check its API first); if it does not, `Ctrl+click` is `not built` and the Done notes say so (the keys still work).
- `SetHighlights`: a decoration layer with a token brush (`SquillAccentSubtleBrush` or the closest existing token), repainted on theme change.

## Out of scope
- Avalonia (P21-25).

## Tasks
- [ ] Text-check tests (P20-38 pattern) for the key names and `SetHighlights`; publish and drive (recipe 3): F12 on an alias, on a CTE, on a table (explorer selection), usages list, dark and light.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- LoomEditor decoration count on a large script: cap highlights at 1,000 ranges.

## Open points (defaults)
- None.

## Suggested commit message
Bind go to definition and find usages in the WinUI editor
