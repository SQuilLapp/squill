# P21-25 - Avalonia: F12, Ctrl+click and usage highlights

Status: todo
Depends on: P21-23, P21-24

## Goal
Same as P21-24 in the Avalonia head; then the README and changelog lines of the feature.

Exists today: `src/Squill.App.Avalonia/Editors/AvaloniaSqlEditor.cs` (`ISqlEditor` implementation), its key handling and highlighting.

## Design decisions (final)
- Same keys and behaviour as P21-24; `Ctrl+click` through the text view's `GetPositionFromPoint` (AvaloniaEdit exposes it); highlights through a background renderer with the theme brush.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests (keys, highlights renderer present); publish and drive (recipe 3).
- [ ] README line in the SQL completion section, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-24.

## Open points (defaults)
- None.

## Suggested commit message
Bind go to definition and find usages in the Avalonia editor
