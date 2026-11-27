# P21-80 - Avalonia: keys through the keymap and the Keymap settings page

Status: todo
Depends on: P21-78, P21-79

## Goal
Same as P21-79 in the Avalonia head (grep `Key.` checks and `KeyBinding`s), then README and changelog lines.

Exists today: Avalonia key handlers and `SettingsWindow.axaml`.

## Design decisions (final)
- Same inventory rule and settings page; `Cmd` displayed on macOS.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README line (settings), `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-79.

## Open points (defaults)
- None.

## Suggested commit message
Route Avalonia shortcuts through the keymap and add the Keymap settings page
