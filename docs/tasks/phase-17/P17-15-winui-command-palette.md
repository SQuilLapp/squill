# P17-15 - WinUI head: the command palette overlay (risky)

Status: done
Depends on: P17-14, P9-09

## Goal
`Ctrl+K` and a click on the title bar search box open the M13 palette over the WinUI window. Recipe decisions 10 to 17; mockup M13 (dimmed window, centered card ~ 880 wide at 1440, search field with `Échap` chip, mode pills, sections, footer) and risk "focus and keys".

## Design decisions (final)
- Host: `src/Squill.App.WinUI/MainWindow.xaml(.cs)` / `MainWindow.Chrome.cs` (the title bar search box, P9-09, is currently a non-functional placeholder: make it a button-like box that calls `Workspace.OpenPaletteCommand`; keep its look and `Ctrl+K` chip). A new control `src/Squill.App.WinUI/Controls/CommandPalette.cs` (code-built like the other controls of the head) is added as the **top-most child of the window's root grid**, spanning it, so it sits above the Win2D grid and the editor; not a `ContentDialog` and not a separate window (a dialog would block the title bar and cannot dim the content).
- Look (tokens only, `visual-spec.md` section 3): scrim `fg-1` at about 45 % alpha (the mockup dims the whole window including the title bar), card radius 12, `bg-overlay`, `shadow-3` through the shared popup style, top offset about 15 % of the window height, search row (icon, text box without border, `Échap` kbd chip), pill row (`Tout`, `> Actions`, `# Objets`, `: Ligne`, `! Historique`; selected pill `accent-soft` with `accent` text and border; the pills call `SetMode`), a virtualized list (`ListView` with grouped sections, or an `ItemsRepeater` over a flattened list with section captions as items), footer (`↑ ↓ naviguer`, `Entrée ouvrir`, `Ctrl+Entrée nouvel onglet`, results and time at the right). Row: 28 high, icon from `IconKey` (map keys to the head's icon resources; add missing glyph paths once to the shared icons), title with the matched `Ranges` in `accent` (a `TextBlock` with `Run`s, mono font for objects and history titles, UI font for actions), muted `Detail`, right side either the connection name with an 8 px environment square (colors of the explorer / tab markers for `ConnectionEnvironment`) or a `kbd` chip for the shortcut, disabled rows in `fg-3`. Selected row `accent-soft`.
- Keys (the risky part, all handled in the control, never reaching the editor): `Ctrl+K` toggles from anywhere in the window (a window-level `KeyboardAccelerator` plus handling the key in `PreviewKeyDown` at the root so the editor, the grid and the explorer cannot swallow it; check LoomEditor and the Win2D grid for a `Ctrl+K` of their own and say what was found); `Up` / `Down` move, `PageUp` / `PageDown` by 8, `Enter` runs, `Ctrl+Enter` runs with `NewTab`, `Esc` closes, `Tab` is trapped inside the palette (focus cycle: text box, pills, list), a pointer click on a row runs it, a click on the scrim closes. On close, focus returns to the control that had it (editor, grid or explorer: remember `FocusManager.GetFocusedElement` at open). While open, nothing behind receives keys or pointer input (the scrim covers it; set `IsHitTestVisible` correctly and test that a typed letter does not reach the editor).
- Typing goes to `PaletteViewModel.Text` two ways with no extra debounce (the view-model cancels stale searches); the text box selects its content on open.
- Head-only actions registered at startup (decision of P17-11): `app.settings` (what the settings menu item runs), `app.newConnection`, `app.checkUpdates`, `app.about`, `app.openDataFolder` where the WinUI head has the matching menu item, ids and `Shortcut` strings exactly as P17-11 lists.
- Automation: `AutomationProperties.Name` on the text box, pills, list and rows (title + detail), list role, selected row announced; the palette traps screen reader focus like a modal.

## Out of scope
- Avalonia (P17-16), mouse-hover selection tuning, an animation.

## Tasks
- [x] `CommandPalette` control, title bar box wiring, `Ctrl+K`, head action registration, shared glyphs.
- [x] Tests only where the head already has a pattern; the rest in "To check by hand".

## Acceptance criteria
- Common acceptance with `Squill.Windows.slnx`; WinUI AOT publish with zero IL2xxx/IL3xxx (`rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64`, in the background; run the exe once, open the palette and close it).
- Captured in light and dark, `fr` and `en`, with `comm` typed on the seeded sample (tools of P9-02), compared with M13; the report says which parts of the mockup have no data (files, symbols) and were left out.
- Stop rule: if the editor control swallows `Ctrl+K` before any window-level handler can see it, stop and report the exact event path with a proposed card fix; never solve it with a global low-level keyboard hook.

## Suggested commit message
Add the command palette to the WinUI head

## To check by hand
- `Ctrl+K` from the editor, the grid, the explorer and an open dialog-less tab; focus returns where it was; no key reaches the editor while open; `Enter` runs, `Ctrl+Enter` new tab; a click outside closes; narrow window; many results scroll; screen reader reads rows; light and dark contrast of the accent letters; `PRODUCTION` squares match the explorer.

## Result
- `Ctrl+K` is caught by `Root.PreviewKeyDown` (tunnelling, so it precedes the editor, grid and explorer) plus a hidden root `KeyboardAccelerator`; neither `PlainSqlEditor` (only `Ctrl+Space`) nor the grid binds `Ctrl+K`, so the stop rule did not apply. While open, the palette swallows every other Ctrl chord except text-editing ones, so `Ctrl+T`, `Ctrl+W`... cannot fire behind the scrim.
- Rows live in a `ScrollViewer` + `StackPanel` (at most ~60 rows: the view-model caps modes at 50), not a virtualized `ListView`; selection is painted from `PaletteViewModel.SelectedIndex`, focus stays in the text box.
- Head actions registered: `app.settings` (`Ctrl+,`), `app.newConnection`, `app.checkUpdates`, `app.about`, `app.openDataFolder`. New glyphs: action, routine, trigger, line. New key `palette.key.escape`.
- Not done here: light/dark and fr/en screenshot comparison with M13 and the by-hand checks (needs an interactive session); files and symbols have no data and are left out.
