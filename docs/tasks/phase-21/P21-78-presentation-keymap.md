# P21-78 - Platform and Presentation: configurable keyboard shortcuts (keymap)

Status: todo
Depends on: none

## Goal
Users can change the keyboard shortcuts of the app's actions, as in DataGrip's keymap and DBeaver's key preferences.

Exists today: shortcuts are hard-coded in each head's key handlers and in the catalogs as display text (`editor.runShortcut` `Ctrl+Enter`, `editor.scriptShortcut`, `plan.explainShortcut`, `QueryTabViewModel.CommitShortcut` `Ctrl+Alt+C`, ...); the palette has an action registry (`PaletteActionRegistry`, `PaletteAction.Shortcut` shown only when every head binds it).

## Design decisions (final)
- `KeyGesture` model (Presentation: modifiers + key name, parse/format, platform display: `Ctrl` vs `Cmd` on macOS) and `KeymapService`: default gestures per action id (the action ids of `PaletteActionRegistry`, extended to every bound key: run, run script, explain, analyze, commit, rollback, format, palette, find, go to definition, ...), user overrides in `keymap.json` (Platform store, source-generated JSON), conflicts detected (same gesture, same scope: `Global`, `Editor`, `Grid`) and reported.
- `KeymapViewModel` for the settings page: list with filter, `Record` (captures the next gesture through a head callback), `Reset`, `Reset all`, conflict warnings; reserved gestures (`Ctrl+C`, `Ctrl+V`, `Ctrl+Z`, `Esc`, `Enter`) cannot be assigned.
- The catalogs' `*Shortcut` display keys are replaced by `KeymapService.Display(actionId)`.
- Heads (P21-79, P21-80) replace their hard-coded key checks by `KeymapService.Matches(actionId, gesture, scope)`.

## Out of scope
- Editor-internal keys of LoomEditor / AvaloniaEdit (cursor movement, selection) - not remappable; keymap presets (DataGrip/VS Code) - open point.

## Tasks
- [ ] Tests: parse/format, defaults equal today's keys (a test listing every current key), override load/save, conflicts, reserved keys, display per platform.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Missing a hard-coded key in the inventory: the heads cards grep for `VirtualKey.` / `Key.` checks and list every one.

## Open points (defaults)
- Owner: ship a "DataGrip keymap" preset (default no).

## Suggested commit message
Add a keymap service with user overrides and conflict detection
