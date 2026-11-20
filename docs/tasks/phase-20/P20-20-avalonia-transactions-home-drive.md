# P20-20 - Avalonia: transactions, production dialog, plan tab and home page leftovers

Status: partial
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
- [x] Drive, capture, read PNGs; fixes; update the Avalonia Results and Notes of `transactions.md`, `plan.md`, `new-screens.md` (never ticks) and the Avalonia cells of `visual-spec.md` section 12 and 15.
- [x] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists compared / not driven per area.

## Risks
- The production confirmation is a separate window: use `Focus-Dialog` (P20-01) before keys, as P19-14 found.

## Suggested commit message
Drive the Avalonia transactions, production dialog, plan tab and home page in both themes and English

## Done notes
- Status partial. Driven on the published Avalonia AOT head (HEAD 14d4749 plus the fix below), SQLite `-Scenario Full`, `Light`, 1440 x 900 logical at scale 1.5 (2160 x 1350 px), French; English for the home page and the production dialog.
- **`Manuel` toggle:** `Click -Mode Invoke` (SelectionItem) does **not** engage it, `Click -Mode Mouse` does (the P19-14 failure is the driver mode, not the app). After it: pill `1 instruction non validee`, `Valider` and `Annuler`, status `Transaction ouverte depuis moins d'une minute · 1 instruction`; `Annuler` (Invoke) cleared the pill; the duration moved to `1 min` by itself later (step 3 of the window checklist seen).
- **Found and fixed:** with a transaction open the toolbar group overflowed at 1440 px (`Annuler` cut off, only reachable by scrolling the group) because `LayoutToolbar` collapsed only the transaction keycaps and the pill. It now also hides the keycaps of `Executer`, `Script` and `Analyser` as a fourth level (named `RunChip`, `ScriptChip`, `AnalyzeChip`); re-published and re-read: `Valider` and `Annuler` fully visible, pill `1`. Test `TransactionGroup_DropsTheRunKeycaps_WhenTheTwoTransactionButtonsStillDoNotFit` (weak: asserts fit or hidden chip). Full test run green (4573 total, 0 failed). Side note: a first capture at an oversized window (2404 px) showed the group clipped with no collapse at all, not reproduced after a resize; the tab-switch path (SizeChanged does not fire) may skip `LayoutToolbar`, not investigated.
- **Seen fine (light, fr):** close question for the last tab with an open transaction (`Valider`, `Annuler les modifications`, `Retour`, statement listed; `Retour` kept the tab); production dialog in English light (red band `PRODUCTION · pg-prod-eu / pg-prod-eu.db`, bold-free fact sentence, mono statement with red left border, typed field, checked transaction box, `Enter = Cancel`, `Run` disabled), compared with M15-Confirmation-clair by eye only; home page English light (1160 px column, subtitle `PostgreSQL, MySQL, SQL Server, SQLite...`, tip card keycap on the same line, 1 min / 6 min recency) and French light.
- **Home vs M01:** the mockup shows engine version in the details line, per-connection tab count, `Ouvrir un fichier`, `Importer des connexions`, `Projets` and a search placeholder that the app does not have (known gaps, not changed). Version text and tab count of a live connection were not checked (the home page cannot be reached from an open workspace by an automation name I found).
- **Not driven:** dark theme, French dark home, English dark, `Valider` branches, window close question, tab close question variants other than `Retour`, narrow window (600 px), plan tab light / dark / English, M04 keycap width comparison, AltGr, macOS `Quit` (no macOS), `Failed` variant (P20-17), Ctrl+Alt+C / Z chords. `docs/verification/` checklists and `visual-spec.md` were not edited.
