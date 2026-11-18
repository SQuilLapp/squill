# P19-20 - WinUI: light theme with a dark system theme, and the production dialog frame

Status: partial
Depends on: P19-12

## Goal
P19-12 drove the WinUI head (published AOT, SQLite profiles) and found two differences it did not fix. See the Notes of `docs/verification/transactions.md`.

## Design decisions (final)
- Light theme while Windows is in dark mode (`settings.json` `theme: Light`): the home page is light, but in the workspace the query toolbar band, the editor and the result message text stay dark or near invisible (`1 ligne affectée` is almost unreadable). Find the code-behind brush lookups (`Res(...)`, `ThemeBrush`) that resolve against the system theme instead of the window's `RequestedTheme`, and make them follow the theme chosen in the settings; add a test where a pattern exists.
- Production dialog (`M15`): the red header is a rounded top of the dialog with a red outline around the whole dialog, `DELETE n'a pas de clause WHERE` is bold, the statement is syntax highlighted (keywords in the accent color), and `Annuler` has the focus ring (it is the default button; today the typed field has it).
- Toolbar `Valider` / `Annuler`: add the `Ctrl+Alt+C` / `Ctrl+Alt+Z` keycaps to the button content as M04 does, shown while there is room (same collapse levels as the pill).

## Out of scope
- Avalonia (P19-14).

## Tasks
- [ ] Fix and test the theme lookups; capture light fr and en again. (lookups fixed; no test, no capture: not driven)
- [x] Dialog frame, bold fact, highlighted statement, focus; keycaps (code only, not driven).
- [x] Update `visual-spec.md` section 12 and the notes of `transactions.md`.

## Acceptance criteria
- Recipe 4 of `debug-polish-recipe.md` (`Squill.Windows.slnx`, AOT publish, app starts); new captures read against M04 and M15 light and dark.

## Suggested commit message
Follow the chosen theme in the WinUI workspace and match the production dialog frame

## Done notes
- Cause of the dark-on-light text: static `Brush(key)` helpers in `QueryTabView`, `FilterBar`, `CellPreviewPanel`, `PlanTable` and `Splitter` read `Application.Current.Resources`, which follows the system theme. They now call `Tokens.Brush(key)`, which reads the variant of `Tokens.Active`, set by `MainWindow` from the chosen theme (and on `ActualThemeChanged`). `QueryTabView.PaintResults` also repaints the editor host, toolbar band, side panel, message, empty text and toolbar icons on a theme change.
- Dialog: danger outline and rounded corners on the dialog and the band, the fact `prod.message.noWhere` (new catalog key) in bold, keywords of the statements in the accent color (`SqlHighlighter`, ANSI tokenizer), `Annuler` always takes the focus.
- Keycaps: `Valider` / `Annuler` already had them; they were the first to disappear. They now collapse after the other chips (level 2, with the pill text).
- Not driven: the AOT head was published (only the known third-party IL2104 / IL3053 warnings) and started, but the window was not driven to the workspace or the dialog, so no light or dark capture was read against M04 / M15, and no automated test was added (no existing test pattern reaches WinUI brushes). The owner or a later session must re-capture.
