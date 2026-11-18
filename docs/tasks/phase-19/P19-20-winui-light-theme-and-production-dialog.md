# P19-20 - WinUI: light theme with a dark system theme, and the production dialog frame

Status: todo
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
- [ ] Fix and test the theme lookups; capture light fr and en again.
- [ ] Dialog frame, bold fact, highlighted statement, focus; keycaps.
- [ ] Update `visual-spec.md` section 12 and the notes of `transactions.md`.

## Acceptance criteria
- Recipe 4 of `debug-polish-recipe.md` (`Squill.Windows.slnx`, AOT publish, app starts); new captures read against M04 and M15 light and dark.

## Suggested commit message
Follow the chosen theme in the WinUI workspace and match the production dialog frame
