# P21-85 - Avalonia: collapsible right properties panel with an icon rail

Status: todo
Depends on: P21-84

## Goal
Same as P21-84 in the Avalonia head, then README, changelog, and the idea line in `docs/ideas.md` marked as done (P21-84, P21-85).

Exists today: Avalonia `MainWindow.axaml` `RadioButton` rail with `OnRailChecked` and `Sidebar.ShowPanel`; `PropertiesMinWindowWidth` in `MainWindow.TitleLayout.cs`.

## Design decisions (final)
- Same header close button and right rail; `PropertiesMinWindowWidth` combined with the user flag in one place.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README line, `CHANGELOG.md` line, `docs/ideas.md` entry marked done.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-84.

## Open points (defaults)
- None.

## Suggested commit message
Let the user close the Avalonia properties panel to a right icon rail
