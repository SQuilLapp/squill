# P17-16 - Avalonia head: the command palette overlay (risky)

Status: todo
Depends on: P17-15

## Goal
The same palette as P17-15 in the Avalonia head. Recipe decisions 10 to 17; mockup M13; risk "focus and keys".

## Design decisions (final)
- Host: `src/Squill.App.Avalonia/MainWindow.axaml(.cs)`. The disabled `CommandSearch` border (comment `Placeholder for the command palette`, `MainWindow.axaml`) becomes an enabled, clickable box (remove `IsEnabled="False"` / `IsHitTestVisible="False"`, remove the placeholder comment) that runs `Workspace.OpenPaletteCommand`; its look is unchanged. New control `src/Squill.App.Avalonia/Controls/CommandPalette.axaml(.cs)` as the **last child of the window's root panel** (above the grid canvas, editor and explorer), scrim plus card as in P17-15, same dimensions, rows, pills, footer, tokens and icon mapping (`IconKey` to the head's icon resources, add missing paths once), matched `Ranges` colored with `Run`/`InlineCollection` in `accent`, selected row `accent-soft`, environment square by `ConnectionEnvironment`.
- Keys: `Ctrl+K` as a `KeyBinding` in `MainWindow.axaml.cs` (next to the existing ones) with `KeyBinding` tunneling checked against AvaloniaEdit and `DataGridCanvas` (neither may consume `Ctrl+K`; say what was found); `Up` / `Down` / `PageUp` / `PageDown` / `Enter` / `Ctrl+Enter` / `Esc` / `Tab` trap exactly as in P17-15; the scrim handles pointer presses (close) and blocks input to the content below; focus returns to the previously focused control on close (`FocusManager.GetFocusedElement()` at open).
- macOS: the shortcut is `Cmd+K` there (`KeyModifiers.Meta`), the same way the head already maps `Ctrl` shortcuts for macOS (see `MainWindow.MacOS.cs`); display strings follow what that file does for other shortcuts.
- Head-only actions registered at startup with the P17-11 ids: settings, new connection, check for updates, about, open data folder, running the same handlers as the menu items.
- Avalonia headless test (`tests/Squill.App.Avalonia.Tests`, existing pattern): opening the palette focuses the text box, typing `comm` shows the expected sections, `Down` then `Enter` runs the second item and closes, `Esc` closes without running, `Ctrl+K` toggles, focus returns to the editor after close, a key typed while open does not change the editor text.

## Out of scope
- New palette features, Linux/macOS manual verification beyond what the owner can run.

## Tasks
- [ ] `CommandPalette` control, title bar box, key bindings, head action registration, headless test.

## Acceptance criteria
- Common acceptance with `Squill.slnx`; Avalonia AOT publish with zero IL2xxx/IL3xxx, run once and close.
- Captured in light and dark, `fr` and `en`, with `comm` typed, compared with M13.
- Stop rule as P17-15 for a swallowed `Ctrl+K`.

## Suggested commit message
Add the command palette to the Avalonia head

## To check by hand
- Same list as P17-15; `Cmd+K` on macOS only if the owner has the machine.
