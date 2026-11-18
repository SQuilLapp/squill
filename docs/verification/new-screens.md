# New screens verification (P18-31): home, schema compare, import, privileges, sessions

Status: **automated done (one failure found), agent passes partial (phase 19: M01 both heads, M11 and M12 WinUI; M09 and M10 not driven, M09-M12 not driven on Avalonia), manual pending.** The owner ticks the boxes below; the `Result` column stays blank until then.

## Automated (done)

- Default run (`rtk bypass dotnet test --solution Squill.slnx -c Release`, 4339 tests): everything passes except two environment-sensitive tests that pass alone: `Squill.Sql.Tests.CompletionTriggerTests.LargeScriptAnswersQuickly` (a timing bound, fails while other test hosts load the machine) and `Squill.App.Avalonia.Tests.LayoutOverlapTests.NoScreen_DrawsTextOverText`, which fails about one run in two (see Bugs found).
- Docker-gated suites (`rtk pwsh eng/run-docker-tests.ps1 -Engine <engine>`, Podman machine, whole test project of each driver):

| Engine | Result |
|---|---|
| PostgreSQL | 141 passed, 0 failed, 0 skipped (includes the sessions, privileges, import and compare flows) |
| SQL Server | 164 passed, 0 failed, 0 skipped |
| MySQL | 158 passed (P18-33 fixed `SchemaCompareFlowTests.CompareAndRunTheScript_LeavesNothingToCompare`, see Bugs found). A first run failed 62 tests only because the Podman VM had `fs.aio-max-nr` exhausted (`io_setup() failed with EAGAIN`, the MySQL container exits at start); raising it to 1048576 inside the VM fixed that. |

- SQLite scenarios (compare, import) run in the default run above and pass.

## Not driven

No capture of M01, M09, M10, M11 or M12 was compared with its PNG in this card, in either head or theme, and no PostgreSQL container was kept with `-Keep` for a window pass. A WinUI launch at the start of the card captured the desktop instead of the window (the capture script copies the screen; the window was not on top), so no visual match is claimed for any of the five screens. Everything in the checklists below is open.

## Known limits

Not recorded in this card: the per-screen limits are in the phase 18 cards (`docs/tasks/phase-18/`); the owner lists any that are visible and surprising in the Note.

## Bugs found

- **MySQL: comparing two databases lists every common table twice (fixed by P18-33).** The MySQL reader sets the schema of each table to its database name, and `SchemaComparer.Key` includes the schema, so `squill.cmp_a` (source) and `squill_cmp_target.cmp_a` (target) never match: the table shows as one Added and one Removed instead of one Modified, and the script would drop and recreate it. PostgreSQL and SQL Server compare within a schema name that is equal on both sides and pass. Fixed by `P18-33`: in the comparer, treat a schema equal to its snapshot's `DefaultSchema` as the same schema on both sides, and make the script qualify names with the target's schema (or none); add a MySQL case to `SchemaComparerTests`.
- **Avalonia: `StatusBar.RefreshEnvironment` is sometimes called from a non-UI thread (fixed by P18-34)** (`StatusBar.axaml.cs:170`, unhandled `InvalidOperationException` in `LayoutOverlapTests`, about one run in two). Proposed card `P18-34`: marshal `Refresh` through the dispatcher when `Dispatcher.UIThread.CheckAccess()` is false.

## How to prepare

- SQLite: `pwsh tools/visual/New-VisualProfile.ps1 -Dir <dir> -Scenario Full [-Theme Light|Dark]`, `SQUILL_DATA_DIR=<dir>`. It creates six profiles with a recent list (M01), `cmp-source` / `cmp-target` with one table only on each side, one extra column, one index and one view that differ (M09), and `people.csv` (40 rows, 3 that fail conversion) with the empty table of profile `import-target` (M10).
- PostgreSQL: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep` prints host, port, user and password. For M12 open a transaction with an update in one tab and a second update on the same row in another; for M11 use the roles the P18-17 scenario creates. Press Enter in that terminal at the end, then `podman ps -a` must show nothing from this run.
- Start the WinUI head with `SQUILL_LANGUAGE=fr`, then `en`, then the Avalonia head. Each screen once in the light theme and once in the dark theme. `SQUILL_NO_UPDATE_CHECK=1`.

## Checklists

Columns: WinUI / Avalonia = tick when it behaves as described in that head. Result: `ok`, `fixed in <commit>`, `bug -> <card>`, `not driven (why)`.

| # | Screen | Step | WinUI | Avalonia | Result |
|---|--------|------|:-----:|:--------:|--------|
| 1 | M01 | Start page lists recent connections with environment pill, shortcut keys, open-tab count; search box opens the palette; `Gérer les connexions`, `Nouvelle connexion`, `Ouvrir un fichier`, `Importer des connexions`, `Console SQL éphémère` work. | [ ] | [ ] | agent (WinUI): bug -> P19-17 (only `Nouvelle connexion` and the search box exist; the other four buttons are not drawn; no open-tab count); recent cards, environment pills and `Ctrl+1` ... `Ctrl+6` chips ok; opening the palette from the search box not driven; agent (Avalonia): bug -> P19-19 (same gaps as WinUI, no engine badge, missing start tiles); recent cards, pills and chips ok |
| 2 | M01 | Projects list, tip with `Astuce suivante` / `Masquer`, `Afficher l'accueil au démarrage` check persists. | [ ] | [ ] | agent (WinUI): tip card with `Astuce suivante` / `Masquer` and the startup check drawn and ok; persistence of the check not driven; projects list not built (visual-spec section 10); agent (Avalonia): tip card and startup check drawn; French tip text clipped -> P19-19; persistence not driven |
| 3 | M09 | Pick source and target: groups, counts, include checkboxes, side-by-side DDL, generated script, `Exécuter le script`; after running, nothing left to compare. | [ ] | [ ] | agent: not driven (no UI automation of the native window in this session: connections cannot be opened and the compare tab cannot be reached by script); agent (Avalonia): not driven (no UI automation of the native window in this session) |
| 4 | M10 | Wizard steps: source file, options, mapping (types, conflicts), error policy, run with progress and outcome. | [ ] | [ ] | agent: not driven (no UI automation of the native window and file picker in this session); agent (Avalonia): not driven (no UI automation of the native window in this session) |
| 5 | M11 | Roles list and privilege matrix per object kind; toggle a cell, see the pending change and the generated statements; apply. | [ ] | [ ] | agent (WinUI, PostgreSQL 17 container): light fr, dark fr, light en driven: roles list, `Schéma` flyout, toggle `INSERT` on `sec_a.t2` gave `GRANT INSERT ON sec_a.t2 TO analyst;` and `1 changement`, `Appliquer` ran it (`has_table_privilege` = true); agent: bug -> P19-18 (matrix shows only `SELECT` and `INSERT`); agent (Avalonia): not driven (no UI automation of the native window in this session) |
| 6 | M12 | Sessions table with state dots, sort and filter, blocking chains, detail pane; cancel and kill with confirmation. | [ ] | [ ] | agent (WinUI, PostgreSQL 17 container): light fr, dark fr, light en, Production light fr driven: dots, blocking chain `BLOQUE 1`, detail pane, `Terminer la session` confirmation on Dev and on Production (type the user name); `Annuler la requête` dialog opened; sort and state tabs not driven; agent: bug -> P19-18 (columns after `Durée` clipped); agent (Avalonia): not driven (no UI automation of the native window in this session) |
| 7 | All | Compare each screen with `M01`, `M09`, `M10`, `M11`, `M12` in both themes; list every visible difference in the Note below. | [ ] | [ ] | agent (WinUI): M01 compared in light, dark, fr, en; M09 and M10 not driven (see rows 3 and 4); agent (Avalonia): M01 compared in French light, French dark, English dark; M09 to M12 not driven |

## Note

Visible differences from the mockups (to be completed by the owner): see below.

P19-09 (agent, WinUI published AOT head, 1440 x 900, capture method Screen with the window on top, SQLite data of `-Scenario Full`): M01 compared with `M01-Accueil-clair-Windows.png` and `M01-Accueil-sombre-Windows.png` in light and dark, in French (both themes) and English (both themes). Matches: palette and both themes, 3 px environment border on the cards, environment pills, `Ctrl+N` / `Ctrl+1` ... `Ctrl+6` chips, `ASTUCE` card with `Astuce suivante` / `Masquer`, startup check, status bar text; English strings present and consistent. Differences (all proposed as card P19-17, none fixed here because each needs layout work and an AOT publish): no engine badge (`PG`, `MY`...) on the cards; no engine / version / open-tab count line; content spans the full window width where the mockup centers a column about 1160 wide; the start column only has `Nouvelle connexion` (no subtitle, no `Ouvrir un fichier`, `Importer des connexions`, `Console SQL éphémère`; the last two and the projects list are listed as not built in `visual-spec.md` section 10); the header has no `SQuilL` centered title and the menu has `Requête` and `Base de données` the mockup does not show; the header search box has no focus ring; no `Mise à jour` / `Pilotes à jour` status text. P19-17 (WinUI) closed the second line (engine, live version, last opened), the open-tab count, the 1160 px centered column, the `Nouvelle connexion` subtitle and the search box focus ring; re-captured in French light and English dark. Known gap: the `Ouvrir un fichier` tile (`Ctrl+O`) is not built because no such command exists in `src/` and phase 19 builds no feature (owner decision of 2026-11-18). M09 and M10 were not driven: nothing in this session can click or type in a native window, so no comparison is claimed for them. Rows 5 and 6: see the next paragraph.

P19-11 (agent, Avalonia published AOT head, Windows only; Linux and macOS not available, nothing claimed for them; 1440 x 900 logical at display scale 1.5, `Screen` capture with the window on top, SQLite data of `-Scenario Full`): M01 compared with `M01-Accueil-clair-Windows.png` / `M01-Accueil-sombre-Windows.png` in French light, French dark and English dark (English light not captured). Matches: palette, both themes, 3 px environment borders, pills, `Ctrl+N` / `Ctrl+1` ... `Ctrl+6` chips, tip card and startup check, status bar; the content is centered in a column (unlike WinUI). Differences: same as WinUI (no engine badge, no engine / version / open-tab line, only `Nouvelle connexion` in `DÉMARRER`, no subtitle, no `SQuilL` title in the header, menu has `Requête` and `Base de données`, no update status text) plus the French tip text `Placez le curseur dans une requête et appu...` is clipped at the right edge of the card (the English text fits). Proposed card P19-19. M09, M10, M11 and M12 on Avalonia were not driven: nothing in this session can click or type in a native window, so no comparison is claimed, and the enum-name subtitle of M10 was not re-checked.

Known limit (P18-33): a view whose definition hard-codes the database name (for example `squill.t`) still differs textually between two MySQL databases.
