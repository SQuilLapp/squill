# P20-15 - WinUI: see the light theme over a dark system, the production dialog, and the transaction leftovers

Status: todo
Depends on: P20-01, P20-02

## Goal
P19-20 changed the light-theme lookups, the production dialog frame (red outline, rounded band, bold `DELETE n'a pas de clause WHERE`, highlighted statement, `Annuler` focus) and the `Valider` / `Annuler` keycap collapse order, "code only, not driven"; P20-02 finishes the lookups. Look at them. Also close what P19-12 left: close questions, AltGr chords, narrow window, plan tab language rows (`transactions.md` 46 `not driven` mentions, `plan.md`). Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: WinUI AOT exe on a machine whose Windows apps theme is **dark** (check `HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` `AppsUseLightTheme`; if it is 1, say so and mark the light-over-dark rows `not driven (system theme is light)`), `settings.json` `theme: Light`, SQLite `-Scenario Full`.
- Light over dark system: home page, workspace (query toolbar band, editor, result message `1 ligne affectée`, side panel, empty text), grid filter bar, search box, palette, cell editor frame, ER and plan canvases: every text readable, no dark island. Compare the workspace with `M03-Workspace-clair-Windows.png`. Repeat with `theme: Dark` on a light system if the machine can be switched without harm (do not change the system theme; skip otherwise).
- Production dialog (`M15`): `DELETE` without `WHERE` on `pg-prod-eu`: frame, bold fact, highlighted statement, `Annuler` focus ring, typed field; light, dark, French, English; compare with `M15-*`. Toolbar `Valider` / `Annuler` keycaps (`Ctrl+Alt+C` / `Ctrl+Alt+Z`) at several window widths against `M04-*`: collapse order (keycaps after the other chips).
- Transaction leftovers: close the window with an open transaction (the three-way close question, `Valider` / `Annuler` / cancel: read the dialog, answer each), close a tab, AltGr chords on a keyboard layout that has AltGr (French AZERTY: `AltGr+Ctrl` combinations must not trigger `Ctrl+Alt+C`; if the machine layout has no AltGr, `not driven`), narrow window (about 600 px), plan tab light / dark / English on SQLite (`Expliquer`, `Graphique`, `Arbre`, `Texte`; `plan.md` language rows 1-3 of the language section). PostgreSQL-only rows stay with P20-14.
- Fixes: small ones here with tests (`ChromeFormatTests`, `EditBarFormatTests`, `ConnectionEditorFormatTests` patterns), bigger ones proposed.

## Out of scope
- Avalonia (P20-20), PostgreSQL rows (P20-14).

## Tasks
- [ ] Drive, capture, read PNGs; fixes; update the Results and Notes of `transactions.md` and `plan.md` (never ticks) and `visual-spec.md` section 12 cells; add `P19-20`'s "not driven" lines as resolved in the card's Done notes.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; report lists compared / not driven per area.

## Risks
- Changing the Windows theme to test is not allowed (system setting): use the machine as it is and record its state.

## Suggested commit message
Drive the WinUI light theme over a dark system, the production dialog and the transaction leftovers
