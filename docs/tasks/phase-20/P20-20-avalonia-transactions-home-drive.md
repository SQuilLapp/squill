# P20-20 - Avalonia: transactions, production dialog, plan tab and home page leftovers

Status: todo
Depends on: P20-15

## Goal
P19-14 drove only the production confirmation window and the SQLite plan tab on Avalonia (dark, French). Not driven: light, English, `Manuel` mode (the UIA click on the toggle did not engage it), the transaction pill, `Valider` / `Annuler`, close questions, the `Failed` variant (PostgreSQL, P20-17), AltGr, macOS `Quit`, the narrow window. P19-19 changed the home page (details line, tab count, subtitle, focus ring, 1160 px column) and never re-captured it. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe, SQLite `-Scenario Full` (`pg-prod-eu` is Production, `sqlserver-dev` Development), light and dark, French; English light; scale 1.5 pixel sizes as in P20-16.
- Read the Done notes of P20-15 first and reuse its findings and fixes. Drive: the `Manuel` toggle (with `Drive-Window.ps1 Click -Mode Mouse` if `Invoke` does not engage it, the P19-14 failure: record which mode works), the pill, `Valider` / `Annuler` and their keycaps at several widths against `M04-*`, the production window (`M15-*`) light and English with the typed field and the `Annuler` default, the close questions (window and tab with an open transaction), the narrow window (about 600 px), the plan tab light / dark / English. AltGr: only on a layout that has it. macOS `Quit` stays `not driven (no macOS)`.
- Home page (M01, `new-screens.md` row 1 and 7): recapture French light, French dark, English dark against `M01-Accueil-*`: details line `moteur · version`, tab count with a live connection (open `local-sqlite` and check the count and the version text, which P19-17 and P19-19 covered by unit tests only), subtitle `PostgreSQL, MySQL, SQL Server, SQLite...`, focus ring on the search button, column width, tip card text wrapping at a narrow width.
- Small fixes with tests (`tests/Squill.App.Avalonia.Tests/HomePageUiTests.cs`, `EditBarUiTests.cs`, `EditorToolbarTests.cs`, `PlanUiTests.cs`); propose bigger ones.

## Out of scope
- WinUI (P20-15), PostgreSQL rows (P20-17), Linux / macOS.

## Tasks
- [ ] Drive, capture, read PNGs; fixes; update the Avalonia Results and Notes of `transactions.md`, `plan.md`, `new-screens.md` (never ticks) and the Avalonia cells of `visual-spec.md` section 12 and 15.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists compared / not driven per area.

## Risks
- The production confirmation is a separate window: use `Focus-Dialog` (P20-01) before keys, as P19-14 found.

## Suggested commit message
Drive the Avalonia transactions, production dialog, plan tab and home page in both themes and English
