# P21-36 - Avalonia: the Files panel and bookmark gutter marks

Status: todo
Depends on: P21-34, P21-35

## Goal
Same as P21-35 in the Avalonia head, then README and changelog lines.

Exists today: `MainWindow.axaml` rail (`RadioButton`s, `OnRailChecked`, `Sidebar.ShowPanel`), `AvaloniaSqlEditor`.

## Design decisions (final)
- Same panel and menus; bookmarks drawn by an AvaloniaEdit left-margin glyph.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README "Script files" section completed with the panel and bookmarks, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-35.

## Open points (defaults)
- None.

## Suggested commit message
Show the Files panel and bookmarks in the Avalonia head
