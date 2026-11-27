# P21-35 - WinUI: the Files panel and bookmark gutter marks

Status: todo
Depends on: P21-34

## Goal
Show the Files panel and the bookmarks in the WinUI head.

Exists today: `MainWindow.xaml` rail (`RailButtonStyle` buttons, `Views/SidebarPanel.cs`), the explorer tree as the tree pattern, `LoomSqlEditor` decorations.

## Design decisions (final)
- A fourth rail button `Files` (new icon `SquillIconFolder` if none exists), the panel with favorites section, tree, filter box and a context menu built in code (no `ItemsSource`).
- Bookmarks: a gutter glyph through LoomEditor's line decoration if it has one; otherwise a full-line background tint (record which in the Done notes).

## Out of scope
- Avalonia (P21-36).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3): add a folder, open a file, favorite it, toggle bookmarks and jump.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- WinUI `TreeView` with a code template: reuse the explorer's approach (P9-26).

## Open points (defaults)
- None.

## Suggested commit message
Show the Files panel and bookmarks in the WinUI head
