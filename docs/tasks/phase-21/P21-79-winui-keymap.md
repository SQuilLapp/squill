# P21-79 - WinUI: keys through the keymap and the Keymap settings page

Status: todo
Depends on: P21-78

## Goal
The WinUI head reads its shortcuts from `KeymapService` and shows the keymap page in Settings.

Exists today: key handlers in `MainWindow.xaml.cs`, `QueryTabView.cs`, the grid and the palette; `SettingsWindow.xaml`.

## Design decisions (final)
- Inventory every hard-coded key check in `src/Squill.App.WinUI` (grep `VirtualKey.`), route each through `Matches`; a text-check test fails if a `VirtualKey.` comparison remains outside an allow-list (editor-internal, dialog `Enter`/`Esc`).
- Settings: a `Keymap` page (list, filter, record by capturing `KeyDown` on a focused box, reset), tooltips and menus show `Display(actionId)`.

## Out of scope
- Avalonia (P21-80).

## Tasks
- [ ] Tests; publish and drive (recipe 3): remap `Run` to `F5`, run with `F5`, conflict warning, reset.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- AltGr chords on French keyboards (P20-15): do not treat `Ctrl+Alt` from AltGr as a gesture.

## Open points (defaults)
- None.

## Suggested commit message
Route WinUI shortcuts through the keymap and add the Keymap settings page
