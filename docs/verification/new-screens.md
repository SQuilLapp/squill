# New screens verification (P18-31): home, schema compare, import, privileges, sessions

Status: **automated done (one failure found), agent window pass not done, manual pending.** The owner ticks the boxes below; the `Result` column stays blank until then.

## Automated (done)

- Default run (`rtk bypass dotnet test --solution Squill.slnx -c Release`, 4339 tests): everything passes except two environment-sensitive tests that pass alone: `Squill.Sql.Tests.CompletionTriggerTests.LargeScriptAnswersQuickly` (a timing bound, fails while other test hosts load the machine) and `Squill.App.Avalonia.Tests.LayoutOverlapTests.NoScreen_DrawsTextOverText`, which fails about one run in two (see Bugs found).
- Docker-gated suites (`rtk pwsh eng/run-docker-tests.ps1 -Engine <engine>`, Podman machine, whole test project of each driver):

| Engine | Result |
|---|---|
| PostgreSQL | 141 passed, 0 failed, 0 skipped (includes the sessions, privileges, import and compare flows) |
| SQL Server | 164 passed, 0 failed, 0 skipped |
| MySQL | 157 passed, **1 failed**: `SchemaCompareFlowTests.CompareAndRunTheScript_LeavesNothingToCompare` (see Bugs found). A first run failed 62 tests only because the Podman VM had `fs.aio-max-nr` exhausted (`io_setup() failed with EAGAIN`, the MySQL container exits at start); raising it to 1048576 inside the VM fixed that. |

- SQLite scenarios (compare, import) run in the default run above and pass.

## Not driven

No capture of M01, M09, M10, M11 or M12 was compared with its PNG in this card, in either head or theme, and no PostgreSQL container was kept with `-Keep` for a window pass. A WinUI launch at the start of the card captured the desktop instead of the window (the capture script copies the screen; the window was not on top), so no visual match is claimed for any of the five screens. Everything in the checklists below is open.

## Known limits

Not recorded in this card: the per-screen limits are in the phase 18 cards (`docs/tasks/phase-18/`); the owner lists any that are visible and surprising in the Note.

## Bugs found

- **MySQL: comparing two databases lists every common table twice.** The MySQL reader sets the schema of each table to its database name, and `SchemaComparer.Key` includes the schema, so `squill.cmp_a` (source) and `squill_cmp_target.cmp_a` (target) never match: the table shows as one Added and one Removed instead of one Modified, and the script would drop and recreate it. PostgreSQL and SQL Server compare within a schema name that is equal on both sides and pass. Proposed card `P18-33`: in the comparer, treat a schema equal to its snapshot's `DefaultSchema` as the same schema on both sides, and make the script qualify names with the target's schema (or none); add a MySQL case to `SchemaComparerTests`.
- **Avalonia: `StatusBar.RefreshEnvironment` is sometimes called from a non-UI thread** (`StatusBar.axaml.cs:170`, unhandled `InvalidOperationException` in `LayoutOverlapTests`, about one run in two). Proposed card `P18-34`: marshal `Refresh` through the dispatcher when `Dispatcher.UIThread.CheckAccess()` is false.

## How to prepare

- SQLite: `pwsh tools/visual/New-VisualProfile.ps1 -Dir <dir>`, `SQUILL_DATA_DIR=<dir>`; open a few SQLite profiles first so the home page has recent connections (M01). Two SQLite files with differences for M09; a CSV and an empty SQLite table for M10.
- PostgreSQL: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep` prints host, port, user and password. For M12 open a transaction with an update in one tab and a second update on the same row in another; for M11 use the roles the P18-17 scenario creates. Press Enter in that terminal at the end, then `podman ps -a` must show nothing from this run.
- Start the WinUI head with `SQUILL_LANGUAGE=fr`, then `en`, then the Avalonia head. Each screen once in the light theme and once in the dark theme. `SQUILL_NO_UPDATE_CHECK=1`.

## Checklists

Columns: WinUI / Avalonia = tick when it behaves as described in that head. Result: `ok`, `fixed in <commit>`, `bug -> <card>`, `not driven (why)`.

| # | Screen | Step | WinUI | Avalonia | Result |
|---|--------|------|:-----:|:--------:|--------|
| 1 | M01 | Start page lists recent connections with environment pill, shortcut keys, open-tab count; search box opens the palette; `Gérer les connexions`, `Nouvelle connexion`, `Ouvrir un fichier`, `Importer des connexions`, `Console SQL éphémère` work. | [ ] | [ ] | not driven |
| 2 | M01 | Projects list, tip with `Astuce suivante` / `Masquer`, `Afficher l'accueil au démarrage` check persists. | [ ] | [ ] | not driven |
| 3 | M09 | Pick source and target: groups, counts, include checkboxes, side-by-side DDL, generated script, `Exécuter le script`; after running, nothing left to compare. | [ ] | [ ] | not driven (MySQL: bug -> P18-33) |
| 4 | M10 | Wizard steps: source file, options, mapping (types, conflicts), error policy, run with progress and outcome. | [ ] | [ ] | not driven |
| 5 | M11 | Roles list and privilege matrix per object kind; toggle a cell, see the pending change and the generated statements; apply. | [ ] | [ ] | not driven |
| 6 | M12 | Sessions table with state dots, sort and filter, blocking chains, detail pane; cancel and kill with confirmation. | [ ] | [ ] | not driven |
| 7 | All | Compare each screen with `M01`, `M09`, `M10`, `M11`, `M12` in both themes; list every visible difference in the Note below. | [ ] | [ ] | not driven |

## Note

Visible differences from the mockups (to be filled by the owner): none recorded, nothing was looked at in a window in this card.
