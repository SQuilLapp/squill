# P20-10 - WinUI: drive schema compare (M09), the import wizard (M10) and the home page persistence

Status: partial
Depends on: P20-01

## Goal
M09 and M10 were never driven in either head (`docs/verification/new-screens.md` rows 3 and 4: `agent: not driven (no UI automation ...)`), and row 2 says the persistence of `Afficher l'accueil au démarrage` was not driven on WinUI. Drive them on the published WinUI AOT head with the data of `New-VisualProfile.ps1 -Scenario Full` and compare with `M09-*` and `M10-*` in `docs/design/mockups/`. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: WinUI AOT exe, SQLite `-Scenario Full`, French light then dark, then English light; `SQUILL_NO_UPDATE_CHECK=1`; `Drive-Window.ps1` (P20-01).
- M09 (row 3): open `cmp-source`, then `Comparer les schémas` from the connection menu (the explorer context menu, `explorer.compareSchemas`), pick `cmp-source` and `cmp-target`: groups and counts match `scenario.sql` (table `archive_2023` only in the source, `avoirs` only in the target, column `produits.poids_kg`, index `ix_factures_numero`, view `v_ca_par_client` different); include checkboxes, the side-by-side DDL, the generated script; run the script with `Exécuter le script` on a **copy** (`cmp_target.db` is rebuilt by the scenario script, re-run it afterwards); on `cmp-target` (Production) the production confirmation appears with the typed database name; after running, the comparison finds nothing left. Compare with `M09-*`.
- M10 (row 4): `import-target` + `people.csv`: the file picker is a native dialog (type the path in its name field with `Drive-Window.ps1 Type`, `Focus-Dialog` first; if it cannot be reached say `not driven (file picker)` and stop this part); steps source, options (separator `;`, header), mapping (types, conflicts), error policy, run with progress and outcome: 37 rows in, rows 12, 25, 33 rejected with their reasons. Compare with `M10-*`.
- Row 2: tick nothing; set the startup check off, close the app, restart: the home page does not open; set it on again (restore it).
- Differences: small ones fixed with a test where a pattern exists; bigger ones proposed as P20-22 onwards.

## Out of scope
- Avalonia (P20-16), PostgreSQL / MySQL / SQL Server compare (their Docker-gated flows ran in P18-31).

## Tasks
- [x] Drive, capture, read PNGs and composites; fixes; update the `Result` cells of rows 2, 3, 4, 7 and the Note of `docs/verification/new-screens.md` (never the ticks), the WinUI cells of `visual-spec.md` sections 15, 16 and 19 (M01, M09, M10).
- [x] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; the report lists compared / not driven per screen, theme and language.

## Risks
- The compare script run modifies `cmp_target.db`: use a scratch `-Dir` and re-create it before each pass.
- The file picker may defeat UI Automation: that is an acceptable `not driven`, record the exact reason.

## Suggested commit message
Drive and compare the WinUI schema compare and import wizard screens

## Done notes
- M09 driven and read in French light and dark (not English); fixed `DdlPane` not redrawing when its lines changed at offset 0 (no test: canvas control, no existing pattern). M10: the native file picker did not accept typed text (driver `Type`, mouse click, `SendKeys`), so only step 1 was read; steps 2 to 5 are `not driven (file picker)`, hence status partial. Home persistence of the startup check driven (unticked, restarted, no home page); re-ticking not done through the UI. Differences from M09 are listed in the Note of `docs/verification/new-screens.md`; none big enough for a P20-22 card. English theme passes and the 37 / 3 import outcome remain for a later pass.
