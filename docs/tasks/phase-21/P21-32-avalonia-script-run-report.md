# P21-32 - Avalonia: the script report result and the error policy menu

Status: todo
Depends on: P21-30, P21-31

## Goal
Same as P21-31 in the Avalonia head, then the README and changelog lines.

Exists today: `QueryTabView.axaml` (`ScriptButton`), `AutoRefreshMenu.cs` (Avalonia) as the menu pattern, `SettingsWindow`.

## Design decisions (final)
- Same behaviour as P21-31 with Avalonia controls (`SplitButton`, radio `MenuItem`s, a window for `Ask`).

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README line (query tab section), `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-31.

## Open points (defaults)
- None.

## Suggested commit message
Show the script run report and the error policy in the Avalonia head
